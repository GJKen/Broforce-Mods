param()
$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Aquabro.cs')
function Read-Method([string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $brace = $source.IndexOf('{', $start); $end = $brace + 1; $depth = 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
$methods = @(
    Read-Method 'private static bool LimitTridentStep('
    Read-Method 'private static void RestoreTridentStep('
    Read-Method 'private static bool AllowOriginalSpearWalls('
) -join "`r`n"
$flight = [regex]::Match($source, 'public class AquabroTridentFlight : MonoBehaviour\s*\{[^}]*\}').Value
if (!$flight) { throw 'Missing production flight fields' }
$harness = @'
using System;
public class MonoBehaviour { }
class Time { public static int frameCount; }
class Mathf { public static float Min(float a,float b) { return Math.Min(a,b); } public static float Max(float a,float b) { return Math.Max(a,b); } }
class GameObject { public object value; public T AddComponent<T>() where T:new() { T v=new T(); value=v; return v; } }
class PredabroSpear {
 public object firedBy; public float life=.3f,X,xI,t; public bool hitWall;
 public GameObject gameObject=new GameObject(); public T GetComponent<T>() where T:class { return gameObject.value as T; }
}
__FLIGHT__
public class Aquabro : MonoBehaviour {
__METHODS__
 static int checks;
 static void Check(bool ok,string name) { checks++; if(!ok) throw new Exception(name); }
 static void Near(float actual,float expected,string name,float epsilon=.00001f) { Check(Math.Abs(actual-expected)<epsilon,name+": "+actual+" != "+expected); }
 static void Step(PredabroSpear s,float wall) {
  float original=s.t,arg=s.t,state;
  bool run=LimitTridentStep(s,ref arg,ref s.t,out state);
  if(run) { Near(arg,s.t,"argument/field timestep"); s.X+=s.xI*s.t;
   if((s.X+Math.Sign(s.xI)*24f-wall)*Math.Sign(s.xI)>=0f) s.hitWall=true;
  }
  RestoreTridentStep(ref s.t,state); Near(s.t,original,"restore original timestep");
 }
 static void Frame(PredabroSpear s,float delta,bool charged,float wall) {
  Time.frameCount++; s.t=Math.Min(delta,.0334f);
  if(charged) s.t*=.3334f;
  for(int i=0;i<(charged?3:1);i++) Step(s,wall);
  if(charged) s.t*=3f;
  s.life-=s.t; // Original RunLife subtracts the restored full frame only once.
 }
 public static string Run() {
  PredabroSpear sample=new PredabroSpear { firedBy=new Aquabro(), X=164.6369f, xI=-664f, life=.001358494f, t=.01111333f };
  Time.frameCount=1; for(int i=0;i<3;i++) Step(sample,120f);
  Near(sample.X,163.7348f,"reported three-substep expiry",.0002f); Near(sample.life,.001358494f,"no substep life deduction");
  Check(!sample.hitWall,"outside-life wall 120 not contacted");
  Check(Math.Abs(sample.GetComponent<AquabroTridentFlight>().usedTime-sample.life)<.0000001f,"one shared substep budget");
  sample.t*=3f; sample.life-=sample.t; Check(sample.life<0f,"original full-frame expiry");
  foreach(float speed in new float[] {-664f,-400f,400f,664f}) foreach(int fps in new int[] {20,30,60,144}) {
   PredabroSpear s=new PredabroSpear { firedBy=new Aquabro(), X=300f, xI=speed };
   float expected=300f+speed*.3f,wall=expected+Math.Sign(speed)*25f;
   int frames=0; while(s.life>0f && frames++<100) Frame(s,1f/fps,Math.Abs(speed)>500f,wall);
   Check(s.life<=0f,"expiry at "+speed+" / "+fps); Near(s.X,expected,"0.3s travel at "+speed+" / "+fps,.002f);
   Check(!s.hitWall,"wall beyond final 24px tip at "+speed+" / "+fps);
  }
  PredabroSpear budget=new PredabroSpear { firedBy=new Aquabro(), xI=400f, life=.04f, t=.01f };
  Time.frameCount++; for(int i=0;i<3;i++) Step(budget,float.PositiveInfinity);
  Check(Math.Abs(budget.GetComponent<AquabroTridentFlight>().usedTime-.03f)<.0000001f,"accumulate same-frame budget");
  budget.life-=.03f; Time.frameCount++; Step(budget,float.PositiveInfinity);
  Near(budget.X,16f,"next-frame budget reset");
  PredabroSpear native=new PredabroSpear { firedBy=new object(), xI=400f, life=.001f, t=.01f };
  Step(native,float.PositiveInfinity); Near(native.X,4f,"non-Aquabro full original step");
  Check(native.GetComponent<AquabroTridentFlight>()==null,"non-Aquabro no flight state");
  Check(AllowOriginalSpearWalls(native),"native horizontal precheck allowed");
  Check(!AllowOriginalSpearWalls(sample),"Aquabro horizontal precheck suppressed");
  PredabroSpear contact=new PredabroSpear { firedBy=new Aquabro(), xI=400f, life=.01f, t=.02f };
  Time.frameCount++; Step(contact,27.5f); Check(contact.hitWall,"wall inside final 24px tip still contacted");
  return "PASS: "+checks+" trident lifetime checks; production methods and flight fields extracted.";
 }
}
'@
$harness = $harness.Replace('__FLIGHT__', $flight).Replace('__METHODS__', $methods)
Add-Type -TypeDefinition $harness -Language CSharp
[Aquabro]::Run()
