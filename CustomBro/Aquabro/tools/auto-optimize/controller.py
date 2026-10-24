"""Run serial Codeg conversations; preserve the worktree baseline on failed attempts."""
import argparse
import ctypes
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import time
import uuid

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
REPO = PROJECT.parent.parent
STATE = PROJECT / 'archive' / 'auto-optimize'
EXCLUDED = {'archive', 'bin', 'obj', '.aquabro-buildrefs', '.aseprite-export', '.layer-probe', '.git', '__pycache__'}
TESTS = ['Test-Spring.ps1', 'Test-Charge.ps1', 'Test-WaterBlastZones.ps1', 'Test-SpriteInitialization.ps1', 'Test-FrameStep.ps1']
OBJECTIVE = 'dll_size'
EFFORT = 'max'
MIN_SAVING_BYTES = 512
BUILD_TOOLS = {'tools/export_skill_effects.lua', 'tools/export_water_effects.lua',
               'tools/SpringPixelMask.cs.in', 'tools/ChargedWaterPixelMask.cs.in'}


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + '.tmp')
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    tmp.replace(path)


def run(command, **kwargs):
    return subprocess.run([str(x) for x in command], capture_output=True, encoding='utf-8', errors='replace', **kwargs)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def cli_path():
    for value in [os.environ.get('AQUABRO_CODEG_CLI'), shutil.which('codeg-cli'),
                  'E:/Program/AI Agent/codeg-server-windows-x64/codeg-cli.exe']:
        if value and Path(value).is_file():
            return str(Path(value).resolve())
    raise RuntimeError('找不到更新后的 codeg-cli.exe')


def shell():
    return shutil.which('pwsh') or shutil.which('powershell') or 'powershell.exe'


def game_running():
    if os.name != 'nt':
        return False
    result = run(['tasklist', '/FI', 'IMAGENAME eq Broforce_beta.exe', '/FO', 'CSV', '/NH'])
    if result.returncode:
        raise RuntimeError('无法确认游戏进程：' + result.stderr)
    return 'broforce_beta.exe' in result.stdout.lower()


def stop_game():
    if game_running():
        run(['taskkill', '/IM', 'Broforce_beta.exe', '/T', '/F'])
    for _ in range(30):
        if not game_running():
            return
        time.sleep(.2)
    raise RuntimeError('游戏仍在运行，拒绝部署或恢复 DLL')


def manifest(root, project=False):
    root = Path(root).resolve()
    if not root.is_dir():
        raise RuntimeError('备份范围不存在：' + str(root))
    output = {}
    for current, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in EXCLUDED and not (Path(current) / d).is_symlink()]
        if not project:
            dirs[:] = []  # Installed runtime files only; never legacy backups or installed source trees.
        for name in files:
            path = Path(current) / name
            if project and HERE in path.parents:
                continue
            if path.is_symlink():
                raise RuntimeError('备份范围含链接：' + str(path))
            output[path.relative_to(root).as_posix()] = sha(path)
    return output


def snapshot(root, backup, project=False):
    root, backup = Path(root).resolve(), Path(backup).resolve()
    hashes = manifest(root, project)
    for name in hashes:
        target = backup / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / name, target)
    write_json(backup.parent / (backup.name + '-manifest.json'), hashes)
    return hashes


def restore(root, backup, hashes, project=False):
    root, backup = Path(root).resolve(), Path(backup).resolve()
    current = manifest(root, project)
    for name in current.keys() - hashes.keys():
        path = (root / name).resolve()
        if not path.is_relative_to(root):
            raise RuntimeError('恢复范围越界')
        path.unlink()
    for name, expected in hashes.items():
        source, target = (backup / name).resolve(), (root / name).resolve()
        if not source.is_relative_to(backup) or not target.is_relative_to(root) or sha(source) != expected:
            raise RuntimeError('备份内容／路径校验失败：' + name)
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or sha(target) != expected:
            shutil.copy2(source, target)
    if manifest(root, project) != hashes:
        raise RuntimeError('恢复后哈希不一致')


def changed(before, after):
    return sorted(x for x in before.keys() | after.keys() if before.get(x) != after.get(x))


def validate_size(output, before, after):
    old, new = Path(before).stat().st_size, Path(after).stat().st_size
    saving = old - new
    if saving < MIN_SAVING_BYTES:
        raise RuntimeError(f'DLL 体积收益不足：{old} -> {new} 字节，至少减少 {MIN_SAVING_BYTES} 字节；请合并相关方案或换方案')
    if output.get('dll_bytes_before') != old or output.get('dll_bytes_after') != new or output.get('saved_bytes') != saving:
        raise RuntimeError('申报 DLL 字节数与实际文件不符')
    return dict(before_bytes=old, after_bytes=new, saved_bytes=saving,
                saved_percent=round(100 * saving / old, 4))


def validate_masks(before, after, folder):
    # The probe writes a versioned binary: all five masks, dimensions, frame lengths and bytes.
    records = []
    root = Path(folder).resolve()
    for value in (before, after):
        path = Path(value).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise RuntimeError('缺少本轮游戏导出的完整蒙版')
        data = path.read_bytes()
        if not data.startswith(b'AQUABRO_MASKS_V1\n') or len(data) < 1024:
            raise RuntimeError('完整蒙版导出格式不正确')
        offset = len(b'AQUABRO_MASKS_V1\n')
        def integer():
            nonlocal offset
            if offset + 4 > len(data):
                raise RuntimeError('蒙版导出被截断')
            number = struct.unpack_from('<i', data, offset)[0]
            offset += 4
            return number
        expected = [('ChargedWaterPixelMask', 64, 6), ('MaelstromPixelMask', 128, 32),
                    ('SharkPixelMask', 128, 59), ('SpringPixelMask', 128, 28),
                    ('TidalWavePixelMask', 128, 16)]
        if integer() != len(expected):
            raise RuntimeError('蒙版导出没有覆盖全部五种蒙版')
        for name, size, count in expected:
            # BinaryWriter uses a 7-bit encoded UTF-8 length; these fixed names fit in one byte.
            encoded = name.encode('utf-8')
            if data[offset:offset + 1 + len(encoded)] != bytes([len(encoded)]) + encoded:
                raise RuntimeError('蒙版名称／顺序不符')
            offset += 1 + len(encoded)
            if integer() != size or integer() != count:
                raise RuntimeError('蒙版尺寸／帧数不符')
            for _ in range(count):
                length = integer()
                if length != size * size // 8 or offset + length > len(data):
                    raise RuntimeError('蒙版帧字节不完整')
                offset += length
        if offset != len(data):
            raise RuntimeError('蒙版导出含额外数据')
        records.append(data)
    if records[0] != records[1]:
        raise RuntimeError('游戏实际解码的蒙版帧／尺寸／字节改变')


def compile_check(output_dir):
    output_dir = Path(output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    result = run([shell(), '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
                  HERE / 'Measure-Build.ps1', '-OutputDirectory', output_dir], cwd=PROJECT, timeout=600)
    (output_dir / 'compile.log').write_text(result.stdout + '\n' + result.stderr, encoding='utf-8')
    if result.returncode or not (output_dir / 'Aquabro.dll').is_file():
        raise RuntimeError('控制器独立复编译失败，请查看 compile.log')
    return output_dir / 'Aquabro.dll'


def validate_evidence(before, after, folder, old_hash, new_hash):
    values = []
    for name, phase, expected in [(before, 'before', old_hash), (after, 'after', new_hash)]:
        path = Path(name).resolve()
        if not path.is_relative_to(Path(folder).resolve()):
            raise RuntimeError('证据文件不在本轮目录')
        data = read_json(path)
        if data.get('phase') != phase or data.get('passed') is not True:
            raise RuntimeError('实机记录未通过')
        if str(data.get('dll_sha256', '')).upper() != expected:
            raise RuntimeError('实机 DLL 哈希与磁盘不符')
        uuid.UUID(data['runtime_mvid'])
        if data.get('character') != 'AquabroMod.Aquabro' or not data.get('scene') or not data.get('mechanisms'):
            raise RuntimeError('实机记录缺少角色／场景／机制不变量')
        if not isinstance(data['mechanisms'], dict) or not data.get('raw_logs'):
            raise RuntimeError('缺少机制及原始实机证据')
        for raw in data['raw_logs']:
            raw = Path(raw).resolve()
            if not raw.is_relative_to(Path(folder).resolve()) or not raw.is_file() or not raw.stat().st_size:
                raise RuntimeError('原始实机证据不存在或为空')
        values.append(data)
    if values[0]['mechanisms'] != values[1]['mechanisms']:
        raise RuntimeError('修改前后机制不变量不一致')
    if values[0]['runtime_mvid'] == values[1]['runtime_mvid']:
        raise RuntimeError('修改后没有加载新程序集')


def pid_alive(pid):
    if os.name == 'nt':
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        handle = kernel.OpenProcess(0x1000, False, int(pid))
        if not handle:
            return False
        code = ctypes.c_ulong()
        kernel.GetExitCodeProcess(ctypes.c_void_p(handle), ctypes.byref(code))
        kernel.CloseHandle(ctypes.c_void_p(handle))
        return code.value == 259
    try:
        os.kill(int(pid), 0)
        return True
    except ProcessLookupError:
        return False


class Controller:
    def __init__(self):
        self.cli = cli_path()
        self.batch = datetime.now().strftime('%Y%m%d-%H%M%S')
        self.root = STATE / self.batch
        self.history = read_json(STATE / 'history.json') if (STATE / 'history.json').exists() else []
        self.run_id = None
        self.conversation = None
        self.initial_bytes = (PROJECT / '_Mod/Aquabro.dll').stat().st_size
        self.deploy = [Path(p).resolve() for p in read_json(PROJECT / 'LocalBuild.json')['DeployDirectories']]
        for path in self.deploy:
            if not path.is_dir() or path.name.lower() not in ('aquabro', '_mod'):
                raise RuntimeError('部署目录不属于预期项目：' + str(path))

    def update(self, phase, message, **extra):
        if hasattr(self, 'initial_bytes'):
            current = (PROJECT / '_Mod/Aquabro.dll').stat().st_size
            extra.update(objective=OBJECTIVE, effort=EFFORT, initial_dll_bytes=self.initial_bytes,
                         current_dll_bytes=current, total_saved_bytes=self.initial_bytes - current)
        write_json(STATE / 'status.json', dict(batch=self.batch, pid=os.getpid(), phase=phase,
                   message=message, updated_at=datetime.now().astimezone().isoformat(), **extra))
        print(message, flush=True)

    def call(self, args, input_text=None):
        return run([self.cli, '--no-color', *args], input=input_text, cwd=REPO, timeout=120)

    def collect(self, round_dir):
        cancelling = False
        while True:
            if (STATE / 'STOP').exists() and not cancelling:
                self.call(['cancel', self.run_id, '--json'])
                cancelling = True
                self.update('stopping', '正在取消本轮；确认代理退出后恢复本轮基线。', run_id=self.run_id)
            try:
                response = self.call(['result', self.run_id, '--output-format', 'json'])
                data = json.loads(response.stdout.strip().splitlines()[-1])
            except (ValueError, IndexError, subprocess.TimeoutExpired):
                time.sleep(5)
                continue  # Never restore concurrently with an agent whose status is unknown.
            if data.get('type') == 'result':
                self.run_id = None
                write_json(round_dir / 'codeg-result.json', data)
                if cancelling:
                    raise InterruptedError('用户已停止；未验收轮次已取消。')
                if data.get('is_error') or data.get('subtype') != 'success':
                    raise RuntimeError('会话未正常完成：' + str(data.get('error', data.get('result', data))))
                output = data.get('structured_output')
                if not isinstance(output, dict):
                    output = json.loads(data['result'])
                write_json(round_dir / 'result.json', output)
                return output
            time.sleep(5)

    def round(self, number, attempt, backup, rejected, confirmation):
        folder = backup.parent / f'attempt-{attempt}'
        evidence = folder / 'evidence'
        evidence.mkdir(parents=True, exist_ok=True)
        handoff = dict(batch=self.batch, round=number, retry=attempt, repo=str(REPO),
                       project_backup=str(backup / 'project'), evidence_directory=str(evidence),
                       objective=OBJECTIVE, minimum_saved_bytes=MIN_SAVING_BYTES,
                       baseline_dll_bytes=(backup / 'project/_Mod/Aquabro.dll').stat().st_size,
                       batch_initial_dll_bytes=self.initial_bytes,
                       independent_no_candidate_confirmation=confirmation,
                       history=self.history[-30:], controller_rejections=rejected)
        prompt = (HERE / 'round.md').read_text(encoding='utf-8')
        prompt += '\n\n## 本轮交接参数\n' + json.dumps(handoff, ensure_ascii=False, indent=2)
        (folder / 'prompt.md').write_text(prompt, encoding='utf-8')
        result = self.call(['run', '--agent', 'codex', '--effort', EFFORT, '--workspace', REPO, '--permissions', 'allow',
                            '--title', f'Aquabro - DLL瘦身 {self.batch} 第{number}轮 方案{attempt}',
                            '--timeout', '2h', '--output-schema', HERE / 'round.schema.json',
                            '--output-format', 'json', '--detach', '--prompt', '-'], input_text=prompt)
        (folder / 'launch.log').write_text(result.stdout + '\n' + result.stderr, encoding='utf-8')
        if result.returncode:
            raise RuntimeError('新建对话失败：' + result.stderr)
        init = json.loads(result.stdout.strip().splitlines()[-1])
        self.run_id = init['run_id']
        self.conversation = init.get('link')
        write_json(folder / 'launch.json', init)
        self.update('running', f'第 {number} 轮在新对话执行，失败先换方案。',
                    round=number, run_id=self.run_id, conversation=init.get('link'))
        output = self.collect(folder)
        output['conversation'] = self.conversation
        return output, folder

    def backup(self, folder):
        stop_game()
        hashes = [snapshot(PROJECT, folder / 'project', True)] + [
            snapshot(path, folder / f'deploy-{i}') for i, path in enumerate(self.deploy)]
        baseline = compile_check(folder.parent / 'baseline-build')
        if baseline.stat().st_size != (folder / 'project/_Mod/Aquabro.dll').stat().st_size:
            raise RuntimeError('当前源码按正式参数编译的大小与部署基线不同；不能把不同构建方式算作瘦身收益')
        return hashes

    def rollback(self, folder, hashes):
        stop_game()
        restore(PROJECT, folder / 'project', hashes[0], True)
        for i, path in enumerate(self.deploy):
            restore(path, folder / f'deploy-{i}', hashes[i + 1])

    def accept(self, output, folder, hashes):
        outcome = output['outcome']
        differences = changed(hashes[0], manifest(PROJECT, True))
        if outcome != 'optimized':
            if outcome not in ('no_candidate', 'alternatives_exhausted', 'blocked'):
                raise RuntimeError('未知轮次结果：' + str(outcome))
            if any(p.startswith(('src/', '_Mod/')) or p in BUILD_TOOLS for p in differences):
                raise RuntimeError('未成功的方案仍有生产代码／发布产物变更')
            if outcome == 'alternatives_exhausted' and not output.get('attempts'):
                raise RuntimeError('放弃候选时未记录替代方案')
            return outcome
        if not any(p.startswith('src/') for p in differences) or not output.get('mechanics_unchanged') or not output.get('game_stopped'):
            raise RuntimeError('没有实际源码优化或缺少机制保持／游戏关闭确认')
        if not any(a.get('result') == 'passed' for a in output['attempts']):
            raise RuntimeError('没有通过的实现方案')
        for name in differences:
            if not (name.startswith(('src/', 'tests/', 'docs/', '_Mod/')) or name == '当前角色实际应用状态.md' or name in BUILD_TOOLS):
                raise RuntimeError('越过允许修改范围：' + name)
            if name.startswith('_Mod/') and not name.endswith('.dll'):
                raise RuntimeError('本次精简不得改变玩法配置／正式贴图：' + name)
            if name == 'src/TridentAtlas.cs':
                raise RuntimeError('本次瘦身不得改变动画帧表：' + name)
        tests_before = {p: digest for p, digest in hashes[0].items() if p.startswith('tests/')}
        if any(not (PROJECT / p).is_file() for p in tests_before):
            raise RuntimeError('既有测试文件被删除')
        report = (REPO / output['report']).resolve()
        if not report.is_relative_to(PROJECT / 'docs') or not report.is_file() or not report.stat().st_size:
            raise RuntimeError('缺少 docs 验收报告')
        before_hash = hashes[0]['_Mod/Aquabro.dll']
        after_hash = sha(PROJECT / '_Mod' / 'Aquabro.dll')
        if before_hash == after_hash:
            raise RuntimeError('生产源码改变但 DLL 未更新')
        backup_dll = folder.parent / 'snapshot/project/_Mod/Aquabro.dll'
        size_metrics = validate_size(output, backup_dll, PROJECT / '_Mod/Aquabro.dll')
        rebuilt = compile_check(folder / 'independent-build')
        if rebuilt.stat().st_size != size_metrics['after_bytes']:
            raise RuntimeError('最终 DLL 大小与相同正式参数的独立复编译不符')
        validate_masks(output['mask_before_record'], output['mask_after_record'], folder / 'evidence')
        write_json(folder / 'size-verification.json', dict(objective=OBJECTIVE, **size_metrics))
        validate_evidence(output['baseline_record'], output['after_record'], folder / 'evidence', before_hash, after_hash)
        if game_running():
            raise RuntimeError('验收结束仍有游戏进程')
        for destination in self.deploy:
            if sha(destination / 'Aquabro.dll') != after_hash:
                raise RuntimeError('部署 DLL 不一致')
            for path in (PROJECT / '_Mod').iterdir():
                if path.is_file() and path.suffix.lower() in ('.png', '.json') and (destination / path.name).exists():
                    if sha(path) != sha(destination / path.name):
                        raise RuntimeError('部署资源不一致：' + path.name)
        self.update('verifying', '前后实机证据一致，控制器正在独立执行五组回归。')
        for test in TESTS:
            result = run([shell(), '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', PROJECT / 'tests' / test], cwd=PROJECT, timeout=600)
            (folder / (test + '.log')).write_text(result.stdout + '\n' + result.stderr, encoding='utf-8')
            if result.returncode or 'PASS' not in result.stdout:
                raise RuntimeError('独立回归失败：' + test)
        return outcome

    def loop(self):
        no_candidates = 0
        (STATE / 'STOP').unlink(missing_ok=True)
        self.update('starting', 'DLL 瘦身已启动：只保留体积实际减少且原机制不变的方案。')
        for number in range(1, 10000):
            if (STATE / 'STOP').exists():
                self.update('stopped', '已停止，已验收轮次保留。')
                return
            backup = self.root / f'round-{number:04d}' / 'snapshot'
            hashes = self.backup(backup)
            rejected, output = [], None
            for attempt in range(1, 4):
                try:
                    output, folder = self.round(number, attempt, backup, rejected, no_candidates > 0)
                    outcome = self.accept(output, folder, hashes)
                    if outcome != 'optimized':
                        self.rollback(backup, hashes)
                    self.history.append(dict(batch=self.batch, round=number, outcome=outcome,
                        candidate=output['candidate'], summary=output['summary'], attempts=output['attempts'],
                        report=output['report'], conversation=output['conversation'], objective=OBJECTIVE,
                        dll_bytes_before=output.get('dll_bytes_before'), dll_bytes_after=output.get('dll_bytes_after'),
                        saved_bytes=output.get('saved_bytes')))
                    write_json(STATE / 'history.json', self.history)
                    if outcome == 'blocked':
                        self.update('blocked', '本轮已恢复；环境阻碍：' + output['summary'])
                        return
                    no_candidates = no_candidates + 1 if outcome == 'no_candidate' else 0
                    if no_candidates >= 2:
                        self.update('complete', '连续两轮独立复查均无可保持机制并达到体积门槛的候选，已完成。', rounds=number,
                                    conversation=output['conversation'])
                        return
                    self.update('accepted', '本轮已验收，下一轮新开对话。' if outcome == 'optimized' else '当前未保留改动，新对话继续找其他候选。',
                                round=number, conversation=output['conversation'])
                    break
                except InterruptedError as error:
                    self.rollback(backup, hashes)
                    self.update('stopped', str(error) + ' 本轮已恢复。')
                    return
                except Exception as error:
                    if self.run_id:
                        self.update('blocked', '尚未确认代理结束，保留备份，不能并发恢复：' + str(error), run_id=self.run_id)
                        return
                    self.rollback(backup, hashes)
                    rejected.append(dict(reason=str(error), previous_result=output))
                    write_json(backup.parent / 'rejections.json', rejected)
                    self.update('retrying', '已恢复本轮基线，将换方案：' + str(error))
            else:
                if output is None:
                    self.update('blocked', '连续三次未取得有效会话结果，本轮已恢复。最近原因：' + rejected[-1]['reason'],
                                rejections_file=str(backup.parent / 'rejections.json'))
                    return
                self.history.append(dict(batch=self.batch, round=number, outcome='alternatives_exhausted',
                    candidate=(output or {}).get('candidate', '未验收候选'), summary='三次方案／复验均未通过，已恢复', rejections=rejected))
                write_json(STATE / 'history.json', self.history)
        self.update('complete', '达到单批保护上限；已验收结果保留。')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['start', 'stop', 'status', 'check'])
    action = parser.parse_args().action
    STATE.mkdir(parents=True, exist_ok=True)
    if action == 'stop':
        (STATE / 'STOP').touch()
        print('已请求停止；取消并恢复当前未验收轮次，保留已验收结果。')
        return
    if action == 'status':
        data = read_json(STATE / 'status.json') if (STATE / 'status.json').exists() else {'message': '尚未启动'}
        if data.get('pid') and not pid_alive(data['pid']) and data.get('phase') not in ('complete', 'stopped', 'blocked', 'failed'):
            data['message'] += '（控制器已退出，请检查备份与会话）'
        print(json.dumps(data, ensure_ascii=False, indent=2))
        return
    if action == 'check':
        result = run([cli_path(), 'status'])
        print(result.stdout + result.stderr)
        if result.returncode:
            raise RuntimeError('Codeg 尚未就绪')
        print('工作目录：' + str(REPO))
        return
    lock = STATE / 'controller.lock'
    try:
        handle = lock.open('x', encoding='utf-8')
    except FileExistsError:
        if pid_alive(read_json(lock)['pid']):
            print('已经在运行，无需重复启动。')
            return
        raise RuntimeError('上次控制器异常退出，保留锁和备份，请先检查 status.json。')
    with handle:
        json.dump({'pid': os.getpid(), 'created': datetime.now().isoformat()}, handle)
    try:
        Controller().loop()
    except Exception as error:
        write_json(STATE / 'status.json', dict(phase='failed', message=str(error), pid=os.getpid()))
        raise
    finally:
        lock.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
