param([string]$SourcePath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $SourcePath) { $SourcePath = Join-Path $projectRoot 'src\Aquabro.cs' }
$source = [System.IO.File]::ReadAllText($SourcePath)

function Read-ProductionMethod([string]$signature, [string]$nextSignature) {
    $start = $source.IndexOf($signature)
    $end = $source.IndexOf($nextSignature, $start)
    if ($start -lt 0 -or $end -le $start) { throw "生产方法未找到：$signature" }
    return $source.Substring($start, $end - $start)
}

$awake = Read-ProductionMethod '        protected override void Awake()' '        private static void ApplyTridentVisual('
$refresh = Read-ProductionMethod '        private void RefreshSpriteTextures()' '        private void SetupTridentPrefabs()'

# 编译实际 Awake/刷新方法。模拟 BroMaker 在基类 Awake 换材质，角色被运输禁用且从未运行 Start。
$harness = @'
using System;
using System.Reflection;

class SpriteSM
{
    public int TextureHeight, CachedHeight, Refreshes, Uploads;
    public float PixelX = 64f, PixelY = 96f, V;
    public SpriteSM(int oldHeight) { TextureHeight = CachedHeight = oldHeight; }
    public void RecalcTexture() { CachedHeight = TextureHeight; Refreshes++; }
    public void CalcUVs() { V = PixelY / CachedHeight; }
    public void UpdateUVs() { Uploads++; }
}
class CustomHero
{
    public bool IncludeBody = true, IncludeGun = true, Enabled = true, Started;
    public SpriteSM sprite, gunSprite;
    protected virtual void Awake()
    {
        if (IncludeBody) { sprite = new SpriteSM(512); sprite.TextureHeight = 640; }
        if (IncludeGun) { gunSprite = new SpriteSM(64); gunSprite.TextureHeight = 288; }
        Enabled = false;
    }
}
class Harmony { public Harmony(string id) {} public void Patch(MethodInfo m, object prefix, object postfix, object transpiler, object finalizer) {} }
class HarmonyMethod { public HarmonyMethod(MethodInfo m) {} }
class Projectile { public void Fire() {} }
class BrocheteMachete { protected void RunTrail() {} }
static class WaterProjectileBarrier { public static void Install(Harmony harmony) {} }
class Aquabro : CustomHero
{
    // Projectile patches were already installed by an earlier hero; test the real per-instance lifecycle.
    static bool tridentVisualPatchInstalled = true;
    static void PatchWaterMethod(Harmony harmony, string method, string prefix) {}
    public void RunAwake() { Awake(); }
    public void RefreshAgain() { RefreshSpriteTextures(); }
__AWAKE__
__REFRESH__
}
class SpriteInitializationTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void VerifySprite(SpriteSM sprite, int height)
    {
        Check(sprite.CachedHeight == height, "Pre-Start sprite still caches the Rambro texture size");
        Check(Math.Abs(sprite.V - 96f / height) < .000001f && sprite.Uploads == 1, "Pre-Start UV was not recalculated and uploaded");
        Check(sprite.PixelX == 64f && sprite.PixelY == 96f, "Refreshing changed the selected animation frame");
    }
    static int Main()
    {
        try
        {
            for (int mask = 0; mask < 4; mask++)
            {
                var hero = new Aquabro { IncludeBody = (mask & 1) != 0, IncludeGun = (mask & 2) != 0 };
                hero.RunAwake();
                Check(!hero.Enabled && !hero.Started, "Test must exercise a disabled hero before Start");
                if (hero.sprite != null) VerifySprite(hero.sprite, 640);
                if (hero.gunSprite != null) VerifySprite(hero.gunSprite, 288);
                hero.RefreshAgain();
                if (hero.sprite != null) Check(hero.sprite.Refreshes == 2 && hero.sprite.CachedHeight == 640, "Later body refresh failed");
                if (hero.gunSprite != null) Check(hero.gunSprite.Refreshes == 2 && hero.gunSprite.CachedHeight == 288, "Later weapon refresh failed");
            }
            Console.WriteLine("PASS: disabled pre-Start hero, body/weapon UVs, new instances, null sprites, repeated refresh");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
'@

$testSource = Join-Path ([System.IO.Path]::GetTempPath()) ('Aquabro-SpriteInit-' + [Guid]::NewGuid().ToString('N') + '.cs')
$testExe = [System.IO.Path]::ChangeExtension($testSource, '.exe')
try {
    [System.IO.File]::WriteAllText($testSource, $harness.Replace('__AWAKE__', $awake).Replace('__REFRESH__', $refresh))
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    & $compiler /nologo /target:exe "/out:$testExe" $testSource
    if ($LASTEXITCODE -ne 0) { throw '贴图初始化回归测试编译失败' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '贴图初始化回归测试失败' }
}
finally {
    foreach ($testPath in @($testSource, $testExe)) {
        if (Test-Path -LiteralPath $testPath) { Remove-Item -LiteralPath $testPath -Force }
    }
}
