param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'src\Aquabro.cs'))
function Read-Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "未找到生产方法：$signature" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1
    $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
$methods = @(
    Read-Method 'private void SetupTridentPrefabs()'
    Read-Method 'protected override void RunFiring()'
    Read-Method 'private void FireTrident()'
    Read-Method 'private static void ApplyTridentVisual('
    Read-Method 'private static int GetAnimationFrame('
    Read-Method 'private static bool AllowOriginalSpearTrail('
    Read-Method 'private static int GetChargeAnimationFrame('
    Read-Method 'private static int GetRunningChargeAnimationFrame('
) -join "`r`n"
$constants = [regex]::Matches($source, 'private const float (FullChargeDuration|ThrowCooldown|ThrowAnimationDuration|MeleeSpeedMultiplier|MeleeHitTime|MeleeDuration|RunningChargeLoopDuration|RunningChargeFrameDuration) = [^;]+;')
$constantText = ($constants | ForEach-Object { $_.Value }) -join "`r`n"
$constantText += "`r`n" + [regex]::Match($source, 'private static readonly float\[\] ChargedFrameDurations = \{[^}]+\};').Value

# 直接编译生产方法。外部对象仅记录发射调用，验证输入时序、原版 prefab 和参数。
$harness = @'
using System;
class Mathf {
 public static float Max(float a, float b) { return Math.Max(a, b); }
 public static int FloorToInt(float x) { return (int)Math.Floor(x); }
 public static int Clamp(int x, int a, int b) { return Math.Max(a, Math.Min(b, x)); }
}
enum HeroType { Predabro }
class MonoBehaviour { }
class Renderer { public object gameObject; public bool enabled = true; }
class Projectile : MonoBehaviour {
    public object gameObject = new object();
    public Renderer Child = new Renderer { gameObject = new object() };
    public SpriteSM Sprite = new SpriteSM();
    public T GetComponent<T>() where T : class { return Sprite as T; }
    public T[] GetComponentsInChildren<T>(bool inactive) where T : class {
        return new T[] { new Renderer { gameObject = gameObject } as T, Child as T };
    }
}
class BrocheteMachete : Projectile { public MonoBehaviour firedBy; }
class PredabroSpear : BrocheteMachete { }
class SpriteSM {
    public class Renderer { public object material; }
    public Renderer meshRender = new Renderer();
    public float Width, Height, PixelWidth, PixelHeight, CellX, CellY;
    public int TextureRefreshes;
    public void SetPixelDimensions(int x, int y) { PixelWidth = x; PixelHeight = y; }
    public void SetSize(float x, float y) { Width = x; Height = y; }
    public void SetLowerLeftPixel(float x, float y) { CellX = x; CellY = y; }
    public void RecalcTexture() { TextureRefreshes++; }
    public void CalcUVs() { }
    public void UpdateUVs() { }
}
class CustomInfo { public string path = "Aquabro"; }
class ResourcesController {
    public static object GetMaterial(string path, string filename) { return path + "/" + filename; }
}
class AudioClip { }
class SoundHolder { public AudioClip[] attackSounds = new AudioClip[1]; }
class Predabro {
    public Projectile projectile = new Projectile(), chargedSpearProectilePrefab = new Projectile();
    public SoundHolder soundHolder = new SoundHolder();
}
class HeroController {
    public static Predabro Prefab = new Predabro();
    public static object GetHeroPrefab(HeroType type) { return Prefab; }
}
class GestureElement { public enum Gestures { None, Other } }
class Transform {
    public Scale localScale = new Scale();
    public class Scale { public float x = 1f; }
}
class ProjectileController {
    public static int Count;
    public static Projectile Last;
    public static float X, Y, Speed;
    public static Projectile SpawnProjectileOverNetwork(Projectile p, object owner, float x, float y,
        float speed, float verticalSpeed, bool synced, int player, bool target, bool immediate, float z) {
        Count++; Last = p; X = x; Y = y; Speed = speed; return p;
    }
}
class Map { public static void DisturbWildLife(float x, float y, float range, int player) { } }
class SimulationBase : MonoBehaviour {
    public float t, fireDelay, avatarTime, avatarFireDelay;
    public int BaseCalls;
    protected virtual void RunFiring() { BaseCalls++; fireDelay -= t; }
}
class AquabroProbe : SimulationBase {
    __CONSTANTS__
    static float[] ChargeLoopDurations = { .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f, .034f, .033f, .033f, .033f };
    static Projectile normalTridentPrefab, chargedTridentPrefab;
    static SoundHolder tridentSoundHolder;
    public float chargeTime, throwDelay, throwCooldown, meleeElapsed, counter, X = 100f, Y = 50f;
    public int health = 1, frame, playerNum;
    public bool fire, wasFire, doingMelee, meleeAttackEmitted, ducking, IsMine = true;
    public bool isOnHelicopter, throwingHeldObject, hasBeenCoverInAcid, Incapacitated;
    public bool usingSpecial, usingPockettedSpecial, usingSpecial2, usingSpecial3, usingSpecial4;
    public bool PoseAllowed = true, GunVisible;
    public int AtlasWrites;
    public object impaledByTransform, strungUpBy, inseminatorUnit;
    public GestureElement.Gestures currentGesture;
    public Transform transform = new Transform();
    public CustomInfo Info = new CustomInfo();
    public AquabroProbe() { SetupTridentPrefabs(); }
    bool IsIncapacitated() { return Incapacitated; }
    bool CanShowTridentPose() { return PoseAllowed; }
    void ActivateGun() { GunVisible = true; }
    void DeactivateGun() { GunVisible = false; }
    void SetGunPosition(float x, float y) { }
    void SetGunAtlasCell(int cell) { AtlasWrites++; }
    void SetGunSprite(int x, int y) { }
    void PerformKnifeMeleeAttack(bool a, bool b) { }
    void CancelMelee() { doingMelee = false; }
    void ChangeFrame() { }
    void PlayAttackSound() { }
    public void Tick(bool held, float delta) { wasFire = fire; fire = held; t = delta; RunFiring(); }
    public static void Visual(Projectile shot, MonoBehaviour owner) { ApplyTridentVisual(shot, owner); }
    public static int WaterFrame(float elapsed, bool charged) { return GetAnimationFrame(elapsed, charged); }
    public static bool Trail(BrocheteMachete shot) { return AllowOriginalSpearTrail(shot); }
    public static int ChargePose(float elapsed, bool running) { return running ? GetRunningChargeAnimationFrame(elapsed) : GetChargeAnimationFrame(elapsed); }
    __METHODS__
}
class ChargeTests {
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static bool Near(float a, float b) { return Math.Abs(a - b) < 0.00001f; }
    static void Shot(float held, bool charged, bool left, bool ducking) {
        ProjectileController.Count = 0;
        var p = new AquabroProbe { ducking = ducking };
        p.transform.localScale.x = left ? -1f : 1f;
        p.Tick(true, held);
        Check(ProjectileController.Count == 0, "长按时提前发射");
        p.Tick(false, 0.001f);
        Check(ProjectileController.Count == 1, "松键没有发射一次");
        Check(object.ReferenceEquals(ProjectileController.Last, charged ? HeroController.Prefab.chargedSpearProectilePrefab : HeroController.Prefab.projectile), "未使用对应原版 prefab");
        float direction = left ? -1f : 1f;
        Check(Near(ProjectileController.Speed, direction * (charged ? 664f : 400f)), "弹速不符合 Predabro");
        Check(Near(ProjectileController.X, 100f + direction * 14f) && Near(ProjectileController.Y, ducking ? 58f : 61f), "发射位置不符合 Predabro");
        Check(Near(p.chargeTime, 0f) && Near(p.fireDelay, 0.4f), "发射后未重置");
        p.Tick(false, 0.02f);
        Check(ProjectileController.Count == 1, "松键重复发射");
    }
    static void Interrupted(int kind) {
        ProjectileController.Count = 0;
        var p = new AquabroProbe();
        p.Tick(true, 1.4f);
        if (kind == 0) p.health = 0;
        if (kind == 1) p.Incapacitated = true;
        if (kind == 2) p.doingMelee = true;
        if (kind == 3) p.usingSpecial = true;
        p.Tick(true, 0.01f);
        p.health = 1; p.Incapacitated = p.doingMelee = p.usingSpecial = false;
        p.Tick(false, 0.01f);
        Check(Near(p.chargeTime, 0f) && ProjectileController.Count == 0, "被打断后仍发射旧蓄力");
    }
    public static int Main() {
        try {
            for (int f = 0; f < 6; f++)
                Check(AquabroProbe.WaterFrame(f * .1f + .05f, true) == f, "水球爆裂末6帧播放顺序错误");
            Check(AquabroProbe.WaterFrame(.601f, true) == -1, "水球爆裂未在600ms结束");
            Check(AquabroProbe.WaterFrame(.01f, false) == 0 && AquabroProbe.WaterFrame(.53f, false) == 11 && AquabroProbe.WaterFrame(.541f, false) == -1, "普通水花时序发生变化");
            foreach (bool running in new bool[] { false, true }) {
                Check(AquabroProbe.ChargePose(0.02f, running) == 1, "起手姿势错误");
                Check(AquabroProbe.ChargePose(0.1f, running) == 2 && AquabroProbe.ChargePose(0.499f, running) == 2, "未满蓄力提前出现水流");
                Check(AquabroProbe.ChargePose(0.5f, running) == 3 && AquabroProbe.ChargePose(0.8f, running) > 3, "满蓄力未播放水流循环");
            }
            Shot(0.02f, false, false, false);
            Shot(0.3f, false, false, false);
            Shot(0.499f, false, true, true);
            Shot(0.5f, true, false, false);
            Shot(2f, true, true, true);
            var p = new AquabroProbe();
            p.Tick(true, 0.1f); p.Tick(false, 0.01f);
            p.Tick(true, 0.199f);
            Check(Near(p.chargeTime, 0f), "投掷间隔内开始蓄力");
            p.Tick(true, 0.002f);
            Check(p.chargeTime > 0f && Near(p.throwDelay, 0f), "200ms 后未开始蓄力，或收招动画仍覆盖蓄力");
            int count = ProjectileController.Count;
            p.Tick(false, 0.01f);
            Check(ProjectileController.Count == count + 1, "连续投掷失败");
            for (int i = 0; i < 4; i++) Interrupted(i);
            var terrain = new AquabroProbe { PoseAllowed = false };
            terrain.Tick(true, 0.5f);
            Check(!terrain.GunVisible && terrain.AtlasWrites == 0, "地形蓄力仍显示旧武器格");
            count = ProjectileController.Count;
            terrain.Tick(false, 0.01f);
            Check(ProjectileController.Count == count + 1, "移除旧蓄力贴图后地形投掷失效");
            p = new AquabroProbe { IsMine = false };
            count = ProjectileController.Count;
            p.Tick(true, 1.4f); p.Tick(false, 0.01f);
            Check(ProjectileController.Count == count, "非拥有端重复生成长矛");
            p = new AquabroProbe();
            p.Tick(true, 0f); p.Tick(false, 0f);
            Check(ProjectileController.Count == count, "零时间步产生攻击");
            Check(p.BaseCalls == 2, "未保留基类开火逻辑");
            var spear = new PredabroSpear();
            AquabroProbe.Visual(spear, p);
            Check(!spear.Child.enabled, "原版前景武器未隐藏");
            spear.firedBy = p;
            Check(!AquabroProbe.Trail(spear), "海王仍生成原版长矛残影");
            Check((string)spear.Sprite.meshRender.material == "Aquabro/trident.png", "未应用海王武器素材");
            Check(spear.Sprite.Width == 32 && spear.Sprite.Height == 32 && spear.Sprite.PixelWidth == 32 && spear.Sprite.PixelHeight == 32 && spear.Sprite.CellX == 0 && spear.Sprite.CellY == 32 && spear.Sprite.TextureRefreshes == 1, "武器尺寸或采样坐标错误");
            var otherOwnerSpear = new PredabroSpear();
            AquabroProbe.Visual(otherOwnerSpear, new MonoBehaviour());
            Check(otherOwnerSpear.Child.enabled && AquabroProbe.Trail(otherOwnerSpear), "影响了其他角色长矛前景或残影");
            Check(otherOwnerSpear.Sprite.meshRender.material == null, "修改了其他角色的长矛");
            var otherProjectile = new Projectile();
            AquabroProbe.Visual(otherProjectile, p);
            Check(otherProjectile.Sprite.meshRender.material == null, "修改了其他类型的投射物");
            Console.WriteLine("PASS: 水球爆裂末6帧/600ms、普通水花时序、短按/满蓄力边界、原版长矛、方向/蹲姿/弹速、连续投掷、打断、拥有端、零时间步");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
'@
$testSource = Join-Path $env:TEMP ('Aquabro-Charge-' + [Guid]::NewGuid().ToString('N') + '.cs')
$testExe = [System.IO.Path]::ChangeExtension($testSource, '.exe')
try {
    [System.IO.File]::WriteAllText($testSource, $harness.Replace('__CONSTANTS__', $constantText).Replace('__METHODS__', $methods).Replace('AquabroProbe', 'Aquabro'))
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    & $compiler /nologo /nowarn:0649,0414 /target:exe "/out:$testExe" $testSource
    if ($LASTEXITCODE -ne 0) { throw '蓄力回归测试编译失败' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '蓄力回归测试失败' }
} finally {
    Remove-Item -LiteralPath $testSource,$testExe -Force -ErrorAction SilentlyContinue
}
