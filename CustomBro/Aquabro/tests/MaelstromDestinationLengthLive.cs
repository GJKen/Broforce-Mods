using System;
using System.Reflection;
using UnityEngine;
public static class MaelstromDestinationLengthLive {
 public static void Main(){
 var hero=HeroController.players[0].character;
 if(hero.GetType().FullName!="AquabroMod.Aquabro" || !TestMod.Plugin.IsGamePaused)throw new Exception("Paused Aquabro required");
 var method=hero.GetType().GetMethod("ConsiderMaelstromDestination",BindingFlags.Instance|BindingFlags.NonPublic);
 float oldX=hero.X,oldY=hero.Y;int count=0,accepted=0;
 ScriptContext.Logger("MVID="+hero.GetType().Assembly.ManifestModule.ModuleVersionId);
 ScriptContext.Logger("SCENE="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
 ScriptContext.Logger("CHARACTER="+hero.GetType().FullName);
 try {hero.SetXY(0,6000);hero.SetPosition();
 foreach(int dir in new int[]{-1,1})foreach(float dy in new float[]{0,12,-12,96,96.01f})foreach(int fixture in new int[]{0,1,2,3,4,5,6}){
 var floor=new GameObject("Destination probe floor");var wall=new GameObject("Destination probe wall");
 try {
 floor.layer=wall.layer=LayerMask.NameToLayer("Ground");
 floor.transform.position=new Vector3(0,5960,0);floor.AddComponent<BoxCollider>().size=new Vector3(500,8,16);
 if(fixture==5)floor.SetActive(false);
 if(fixture==6)floor.transform.position=new Vector3(0,5900,0);
 var col=wall.AddComponent<BoxCollider>();col.size=new Vector3(.125f,220,16);
 if(fixture==0 || fixture>=5)wall.SetActive(false);
 if(fixture==2)col.isTrigger=true;
 if(fixture==3)col.enabled=false;
 if(fixture==4)wall.AddComponent<DamageRelay>().unit=hero;
 foreach(float distance in new float[]{0,.99f,1,1.01f,8,64,180,180.01f})foreach(float score in new float[]{float.PositiveInfinity,0,4096})foreach(int side in new int[]{-1,1}){
 float tx=dir*distance,ty=6000+dy;wall.transform.position=new Vector3(tx*.5f,6000,0);
 object[] args={ (float)(dir*side),tx,ty,new Vector2(777,888),score};method.Invoke(hero,args);
 Vector2 p=(Vector2)args[3];float s=(float)args[4];bool ok=p.x!=777;
 if(ok)accepted++;
 bool eligible=side==1 && distance<=180 && Mathf.Abs(dy)<=96 && distance*distance+dy*dy<score;
 if(!eligible && ok && distance!=0)throw new Exception("Range/facing/score filter lost");
 if((fixture==5 || fixture==6) && ok)throw new Exception("Missing floor accepted");
 if(fixture==0 && dy==0 && distance==64 && side==1 && float.IsPositiveInfinity(score) && (!ok || p.y!=5964 || s!=4096))throw new Exception("Open floor selection failed");
 if(fixture==1 && dy==0 && distance==64 && side==1 && float.IsPositiveInfinity(score) && ok)throw new Exception("Wall failed");
 ScriptContext.Logger("CASE|"+dir+"|"+dy+"|"+fixture+"|"+distance.ToString("R")+"|"+score+"|"+side+"|"+p.x.ToString("R")+"|"+p.y.ToString("R")+"|"+s.ToString("R"));count++;
 }
 }finally{UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(floor);}
 }
 if(accepted==0 || count!=3360)throw new Exception("Coverage failed "+count);
 ScriptContext.Logger("COMPLETE|queries="+count+"|accepted="+accepted);
 }finally{hero.SetXY(oldX,oldY);hero.SetPosition();}
 }
}
