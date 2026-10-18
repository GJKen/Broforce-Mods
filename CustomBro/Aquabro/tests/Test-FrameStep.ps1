param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = [System.IO.File]::ReadAllText((Join-Path $projectRoot 'src\Aquabro.cs'))
$start = $source.IndexOf('        protected override void SetDeltaTime()')
$end = $source.IndexOf('        private void SetTraversalFrame(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Aquabro 时间步与 Update 方法未找到' }
$methods = $source.Substring($start, $end - $start)

# 直接编译生产方法；基类模拟 TestMod 跳过更新及放行单步，保留基类动画换帧时机。
$harness = @'
using System;
enum ActionState { Idle, Running, ClimbingLadder }
static class Mathf { public static float Max(float a,float b) { return Math.Max(a,b); } }
class SimulationBase
{
    public float t = 0.02f, lastT, NextDelta = 1f / 60f;
    public bool Frozen, Boosted;
    protected virtual void SetDeltaTime() { lastT = t; t = NextDelta; }
    protected virtual void SetHighFiveBoostDeltaTime() { t = NextDelta * 1.2f; }
    protected virtual void Update()
    {
        if (Frozen) return;
        if (Boosted) SetHighFiveBoostDeltaTime(); else SetDeltaTime();
    }
}
class AquabroProbe : SimulationBase
{
    public bool doingMelee;
    public bool animationTimeStepReady;
    public float counter;
    public int health = 1, frame, BodySlot = 111, VisualCalls, ChangedFrames;
    public ActionState actionState = ActionState.Idle;
    bool AnimateTridentAttack() { VisualCalls++; return false; }
    void AnimateCustomMelee() { VisualCalls++; }
    void SyncCrouchWeapon() { VisualCalls++; }
    void RefreshSkillHud() {}
    void TickSkillPreview(float delta) {}
    void SyncLadderWeapon() { VisualCalls++; }
    bool CanShowTridentPose() { return true; }
    void SyncMovementWeapon(float offset) { VisualCalls++; }
    void SetMovementFrame(int slot, float offset) { BodySlot = slot; VisualCalls++; }
    void ChangeFrame() { ChangedFrames++; }
    public void Tick() { Update(); }
__PRODUCTION_METHODS__
}
class FrameStepTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static bool Near(float a, float b) { return Math.Abs(a - b) < 0.000001f; }
    static void FrozenMovement(bool running)
    {
        var p = new AquabroProbe { Frozen = true, actionState = running ? ActionState.Running : ActionState.Idle };
        for (int i = 0; i < 120; i++) p.Tick();
        Check(p.VisualCalls == 0 && !p.animationTimeStepReady, "Frozen pose was rewritten");
        Check(Near(p.t, 0.02f), "Previous delta was discarded");
        p.Frozen = false;
        p.Tick();
        Check(Near(p.lastT, 0.02f), "Moving-platform lastT changed");
        Check(p.animationTimeStepReady && p.VisualCalls > 0, "Step skipped animation");
        int calls = p.VisualCalls;
        p.Frozen = true;
        for (int i = 0; i < 120; i++) p.Tick();
        Check(p.VisualCalls == calls, "Animation continued between steps");
        p.Frozen = false;
        p.Tick();
        Check(p.VisualCalls > calls, "Second step skipped animation");
    }
    public static int Main()
    {
        try
        {
            FrozenMovement(false);
            FrozenMovement(true);
            var zero = new AquabroProbe { NextDelta = 0f, doingMelee = true };
            zero.Tick();
            Check(zero.VisualCalls == 0, "Zero-time animation changed");
            var boosted = new AquabroProbe { Boosted = true };
            boosted.Tick();
            Check(boosted.animationTimeStepReady && boosted.VisualCalls > 0, "Boosted time step was skipped");
            int calls = boosted.VisualCalls;
            boosted.Frozen = true;
            boosted.Tick();
            Check(boosted.VisualCalls == calls, "Boosted animation leaked while frozen");
            Console.WriteLine("PASS: standing/running freeze and frame steps, lastT, zero delta, boost");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
'@

$testSource = Join-Path $env:TEMP ('Aquabro-FrameStep-' + [Guid]::NewGuid().ToString('N') + '.cs')
$testExe = [System.IO.Path]::ChangeExtension($testSource, '.exe')
try {
    [System.IO.File]::WriteAllText($testSource, $harness.Replace('__PRODUCTION_METHODS__', $methods))
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v3.5\csc.exe'
    & $compiler /nologo /target:exe "/out:$testExe" $testSource
    if ($LASTEXITCODE -ne 0) { throw '时间步回归测试编译失败' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw '时间步回归测试失败' }
}
finally {
    foreach ($path in @($testSource, $testExe)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
}
