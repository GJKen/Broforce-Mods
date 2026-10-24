import importlib.util
import json
import struct
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('controller', Path(__file__).with_name('controller.py'))
c = importlib.util.module_from_spec(spec)
spec.loader.exec_module(c)


class RecoveryTests(unittest.TestCase):
    def test_every_fresh_round_and_retry_requests_max_effort(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            backup = root / 'snapshot'
            (backup / 'project/_Mod').mkdir(parents=True)
            (backup / 'project/_Mod/Aquabro.dll').write_bytes(b'baseline')
            instance = object.__new__(c.Controller)
            instance.batch, instance.history, instance.initial_bytes = 'test', [], 8
            init = dict(run_id='test-run', link='codeg://session/test')
            response = c.subprocess.CompletedProcess([], 0, json.dumps(init), '')
            with patch.object(instance, 'call', return_value=response) as launch:
                with patch.object(instance, 'collect', return_value={}), patch.object(instance, 'update'):
                    for attempt in range(1, 4):
                        instance.round(1, attempt, backup, [], False)
                self.assertEqual(launch.call_count, 3)
                for call in launch.call_args_list:
                    args = call.args[0]
                    self.assertEqual(args[args.index('--effort') + 1], 'max')
                    self.assertEqual(args[args.index('--prompt') + 1], '-')
                    self.assertIn('本轮交接参数', call.kwargs['input_text'])

    def test_windows_long_unicode_prompt_uses_utf8_stdin(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            backup = root / 'snapshot'
            (backup / 'project/_Mod').mkdir(parents=True)
            (backup / 'project/_Mod/Aquabro.dll').write_bytes(b'baseline')
            instance = object.__new__(c.Controller)
            instance.cli, instance.batch, instance.initial_bytes = 'codeg-cli.exe', 'test', 8
            instance.history = [dict(summary='保持机制并减少DLL体积\n' * 10000)]
            init = dict(run_id='test-run', link='codeg://session/test')
            response = c.subprocess.CompletedProcess([], 0, json.dumps(init), '')
            with patch.object(c.subprocess, 'run', return_value=response) as process:
                with patch.object(instance, 'collect', return_value={}), patch.object(instance, 'update'):
                    instance.round(2, 2, backup, [], False)
            command = process.call_args.args[0]
            prompt = process.call_args.kwargs['input']
            self.assertGreater(len(prompt), 32767)
            self.assertLess(len(c.subprocess.list2cmdline(command)), 2000)
            self.assertEqual(process.call_args.kwargs['encoding'], 'utf-8')
            self.assertEqual(command[command.index('--prompt') + 1], '-')
            self.assertEqual(prompt, (backup.parent / 'attempt-2/prompt.md').read_text(encoding='utf-8'))

    def test_size_gate_rejects_same_larger_small_and_false_claims(self):
        with tempfile.TemporaryDirectory() as tmp:
            before, after = Path(tmp) / 'before.dll', Path(tmp) / 'after.dll'
            before.write_bytes(b'x' * 8192)
            for size in (8192, 8704, 8191):
                after.write_bytes(b'y' * size)
                with self.assertRaisesRegex(RuntimeError, '体积收益不足'):
                    c.validate_size(dict(dll_bytes_before=8192, dll_bytes_after=size, saved_bytes=8192-size), before, after)
            after.write_bytes(b'y' * 7680)
            output = dict(dll_bytes_before=8192, dll_bytes_after=7680, saved_bytes=512)
            self.assertEqual(c.validate_size(output, before, after)['saved_bytes'], 512)
            with self.assertRaisesRegex(RuntimeError, '实际文件不符'):
                c.validate_size(dict(output, saved_bytes=1024), before, after)

    def test_complete_mask_comparison_rejects_changed_and_truncated_data(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            data = bytearray(b'AQUABRO_MASKS_V1\n' + struct.pack('<i', 5))
            for name, size, count in [('ChargedWaterPixelMask',64,6), ('MaelstromPixelMask',128,32),
                                      ('SharkPixelMask',128,59), ('SpringPixelMask',128,28), ('TidalWavePixelMask',128,16)]:
                encoded = name.encode('utf-8')
                data.extend(bytes([len(encoded)]) + encoded + struct.pack('<ii', size, count))
                for _ in range(count):
                    length = size * size // 8
                    data.extend(struct.pack('<i', length) + b'\x55' * length)
            before, after = folder / 'before.bin', folder / 'after.bin'
            before.write_bytes(data); after.write_bytes(data)
            c.validate_masks(before, after, folder)
            data[-1] ^= 1
            after.write_bytes(data)
            with self.assertRaisesRegex(RuntimeError, '字节改变'):
                c.validate_masks(before, after, folder)
            after.write_bytes(data[:-1])
            with self.assertRaisesRegex(RuntimeError, '字节不完整'):
                c.validate_masks(before, after, folder)

    def test_insufficient_size_rolls_back_and_tries_another_session(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(c, 'STATE', Path(tmp)):
            folder = Path(tmp)
            before, after = folder / 'before.dll', folder / 'after.dll'
            before.write_bytes(b'x' * 8192); after.write_bytes(b'y' * 8192)
            instance = object.__new__(c.Controller)
            instance.batch, instance.root, instance.history, instance.run_id = 'test', folder / 'test', [], None
            instance.backup = lambda _: [{}]
            instance.update = lambda *a, **kw: None
            bad = dict(outcome='optimized', candidate='no size reduction', summary='', attempts=[], report='', conversation='one',
                       dll_bytes_before=8192, dll_bytes_after=8192, saved_bytes=0)
            none = dict(outcome='no_candidate', candidate='', summary='', attempts=[], report='', conversation='next')
            def accept(output, *args):
                if output['outcome'] == 'optimized':
                    c.validate_size(output, before, after)
                return output['outcome']
            with patch.object(instance, 'round', side_effect=[(bad,folder),(none,folder),(none,folder)]) as launch:
                with patch.object(instance, 'accept', side_effect=accept), patch.object(instance, 'rollback') as rollback:
                    instance.loop()
                self.assertEqual(launch.call_count, 3)
                self.assertEqual(rollback.call_count, 3)
            self.assertEqual(instance.history[0]['outcome'], 'no_candidate')

    def test_restore_preserves_uncommitted_bytes_removes_only_new_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, backup = Path(tmp) / 'project', Path(tmp) / 'backup'
            (root / 'src').mkdir(parents=True)
            (root / 'src' / 'Aquabro.cs').write_bytes(b'user uncommitted baseline\r\n')
            (root / 'archive').mkdir()
            (root / 'archive' / 'evidence.json').write_text('keep')
            before = c.snapshot(root, backup, project=True)
            (root / 'src' / 'Aquabro.cs').write_text('failed optimization')
            (root / 'src' / 'new.cs').write_text('new')
            c.restore(root, backup, before, project=True)
            self.assertEqual((root / 'src' / 'Aquabro.cs').read_bytes(), b'user uncommitted baseline\r\n')
            self.assertFalse((root / 'src' / 'new.cs').exists())
            self.assertEqual((root / 'archive' / 'evidence.json').read_text(), 'keep')

    def test_deploy_restore_does_not_scan_or_remove_legacy_directories(self):
        with tempfile.TemporaryDirectory() as tmp:
            root, backup = Path(tmp) / 'deploy', Path(tmp) / 'backup'
            (root / 'legacy').mkdir(parents=True)
            (root / 'Aquabro.dll').write_bytes(b'original')
            (root / 'legacy' / 'unrelated.txt').write_text('keep')
            before = c.snapshot(root, backup)
            self.assertEqual(set(before), {'Aquabro.dll'})
            (root / 'Aquabro.dll').write_bytes(b'failed')
            c.restore(root, backup, before)
            self.assertEqual((root / 'Aquabro.dll').read_bytes(), b'original')
            self.assertEqual((root / 'legacy' / 'unrelated.txt').read_text(), 'keep')

    def test_mechanics_mismatch_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            raw = folder / 'mcp.json'
            raw.write_text('{"result":"actual test output"}')
            before = dict(phase='before', dll_sha256='A', runtime_mvid='00000000-0000-0000-0000-000000000001',
                          scene='test', character='AquabroMod.Aquabro', mechanisms={'damage': 26}, passed=True, raw_logs=[str(raw)])
            after = dict(before, phase='after', dll_sha256='B', runtime_mvid='00000000-0000-0000-0000-000000000002')
            c.write_json(folder / 'before.json', before)
            c.write_json(folder / 'after.json', after)
            c.validate_evidence(folder / 'before.json', folder / 'after.json', folder, 'A', 'B')
            after['mechanisms'] = {'damage': 25}
            c.write_json(folder / 'after.json', after)
            with self.assertRaisesRegex(RuntimeError, '不变量不一致'):
                c.validate_evidence(folder / 'before.json', folder / 'after.json', folder, 'A', 'B')

    def test_unavailable_sessions_restore_and_stop_infrastructure_retry(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(c, 'STATE', Path(tmp)):
            instance = object.__new__(c.Controller)
            instance.batch, instance.root, instance.history, instance.run_id = 'test', Path(tmp) / 'test', [], None
            instance.backup = lambda _: [{}]
            with patch.object(instance, 'round', side_effect=RuntimeError('Codeg unavailable')) as launch:
                with patch.object(instance, 'rollback') as rollback, patch.object(instance, 'update') as update:
                    instance.loop()
                self.assertEqual(launch.call_count, 3)
                self.assertEqual(rollback.call_count, 3)
                self.assertEqual(update.call_args.args[0], 'blocked')

    def test_failed_acceptance_rolls_back_and_tries_next_scheme(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(c, 'STATE', Path(tmp)):
            instance = object.__new__(c.Controller)
            instance.batch, instance.root, instance.history, instance.run_id = 'test', Path(tmp) / 'test', [], None
            outcomes = iter([dict(outcome='optimized', candidate='same candidate', summary='first', attempts=[], report='', conversation='one'),
                             dict(outcome='no_candidate', candidate='', summary='checked', attempts=[], report='', conversation='two'),
                             dict(outcome='no_candidate', candidate='', summary='confirmed', attempts=[], report='', conversation='three')])
            instance.backup = lambda _: [{}]
            instance.update = lambda *args, **kwargs: None
            instance.round = lambda *args: (next(outcomes), Path(tmp))
            with patch.object(instance, 'rollback') as rollback:
                with patch.object(instance, 'accept', side_effect=[RuntimeError('first scheme broke mechanics'), 'no_candidate', 'no_candidate']) as accept:
                    instance.loop()
                self.assertEqual(accept.call_count, 3)
                self.assertEqual(rollback.call_count, 3)
                self.assertEqual([x['outcome'] for x in instance.history], ['no_candidate', 'no_candidate'])


if __name__ == '__main__':
    unittest.main()
