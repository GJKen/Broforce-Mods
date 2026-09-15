using System;
using System.Collections.Generic;
using Aquabro;
using UnityEngine;

// 只替代引擎边界；编译并执行实际的 RisingWaveDamage.cs。
internal static class RisingWaveDamageTests
{
    private static int assertions;
    private static MonoBehaviour sender;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void Reset()
    {
        sender = new MonoBehaviour();
        Map.units = new List<Unit>();
        Physics.colliders = new Collider[0];
        MapController.hits.Clear();
        Rogueforce.ValueOrchestrator.multiplier = 1;
    }

    private static Collider Part(Unit target)
    {
        return new Collider(new GameObject(new DamageRelay { unit = target }));
    }

    private static void Hit()
    {
        RisingWaveDamage.HitRelays(sender, 0, 3, DamageType.Normal, 0f, 0f, 25f);
    }

    private static void RelayDamageAndModifiers()
    {
        Reset();
        Unit boss = new Unit();
        Collider head = Part(boss);
        Physics.colliders = new Collider[] { head };
        Rogueforce.ValueOrchestrator.multiplier = 2;
        Hit();
        Check(boss.health == 94, "A relay-only boss must receive modified wave damage.");
        Check(MapController.hits.Count == 1, "One part must produce one damage request.");
        HitRequest hit = MapController.hits[0];
        Check(hit.receiver == head.gameObject, "Damage must be sent to the part, not the boss root.");
        Check(hit.sender == sender && hit.damage == 6, "The original owner and damage modifier must be retained.");
        Check(hit.type == DamageType.Normal && hit.xForce == 25f, "The native relay must receive the wave damage type and force.");
        Check(Physics.lastRadius == 12f && Physics.lastMask == Map.groundLayer,
            "Part hits must use the native wave radius and solid-object layers.");
    }

    private static void ExistingUnitNotDoubled()
    {
        Reset();
        Unit target = new Unit();
        Map.units.Add(target);
        Physics.colliders = new Collider[] { Part(target) };
        Hit();
        Check(MapController.hits.Count == 0, "A unit already covered by Map.HitUnits must not be damaged twice.");
        target.X = 200f;
        target.Y = 200f;
        Hit();
        Check(target.health == 97, "A part far from a registered boss root must still be hittable.");
    }

    private static void DuplicateColliders()
    {
        Reset();
        Unit boss = new Unit();
        Collider first = Part(boss);
        Physics.colliders = new Collider[] { first, new Collider(first.gameObject) };
        Hit();
        Check(MapController.hits.Count == 1 && boss.health == 97,
            "Two colliders on one relay must not multiply a wave-node hit.");
    }

    private static void ArmorUsesNativeRelay()
    {
        Reset();
        Unit boss = new Unit();
        Collider shell = Part(boss);
        shell.gameObject.relay.immunity = DamageType.Normal;
        Collider head = Part(boss);
        Physics.colliders = new Collider[] { shell, head };
        Hit();
        Check(MapController.hits.Count == 2, "An immune part must not suppress a different vulnerable part.");
        Check(boss.health == 97, "The relay must retain its immunity behavior.");
        Reset();
        boss = new Unit();
        head = Part(boss);
        head.gameObject.relay.shieldActive = true;
        Physics.colliders = new Collider[] { head };
        Hit();
        Check(MapController.hits.Count == 1 && boss.health == 100,
            "Native part shielding must not be bypassed by direct root damage.");
    }

    private static void TargetFilters()
    {
        Reset();
        Unit owner = new Unit();
        sender = owner;
        Unit friendly = new Unit { playerNum = 0 };
        Unit dead = new Unit { health = 0 };
        Unit invulnerable = new Unit { invulnerable = true };
        Physics.colliders = new Collider[]
        {
            Part(owner), Part(friendly), Part(dead), Part(invulnerable), Part(null),
            new Collider(new GameObject(null))
        };
        Hit();
        Check(MapController.hits.Count == 0, "Owner, friendlies, dead units, invulnerable units and scenery must be skipped.");
    }

    private static void RangeAndMissingOwner()
    {
        Reset();
        Unit boss = new Unit();
        Collider head = Part(boss);
        head.center = new Vector3(13f, 0f, 0f);
        Physics.colliders = new Collider[] { head };
        Hit();
        Check(boss.health == 100, "A part outside the wave node must not receive damage.");
        head.center = new Vector3(12f, 0f, 0f);
        Hit();
        Check(boss.health == 97, "A touching part at the native hit radius must receive damage.");
        sender = null;
        Hit();
        Check(boss.health == 97, "A missing owner must not create unowned damage.");
    }

    public static int Main()
    {
        RelayDamageAndModifiers();
        ExistingUnitNotDoubled();
        DuplicateColliders();
        ArmorUsesNativeRelay();
        TargetFilters();
        RangeAndMissingOwner();
        Console.WriteLine("PASS: 6 wave damage scenarios, " + assertions + " assertions.");
        return 0;
    }
}

namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public static class Mathf { public static float Abs(float value) { return Math.Abs(value); } }
    public class GameObject
    {
        public DamageRelay relay;
        public GameObject(DamageRelay relay) { this.relay = relay; }
    }
    public class Collider
    {
        public GameObject gameObject;
        public Vector3 center;
        public Collider(GameObject gameObject) { this.gameObject = gameObject; }
        public T GetComponent<T>() where T : class { return gameObject.relay as T; }
    }
    public static class Physics
    {
        public static Collider[] colliders;
        public static float lastRadius;
        public static int lastMask;
        public static Collider[] OverlapSphere(Vector3 center, float radius, int mask)
        {
            lastRadius = radius;
            lastMask = mask;
            List<Collider> hits = new List<Collider>();
            foreach (Collider collider in colliders)
            {
                float dx = center.x - collider.center.x;
                float dy = center.y - collider.center.y;
                if (dx * dx + dy * dy <= radius * radius) hits.Add(collider);
            }
            return hits.ToArray();
        }
    }
}

public enum DamageType { Normal, Drill }
public class Unit : MonoBehaviour
{
    public int health = 100;
    public int playerNum = -1;
    public bool invulnerable;
    public float X, Y;
    public float width = 8f, height = 8f;
}
public class DamageRelay : MonoBehaviour
{
    public Unit unit;
    public DamageType immunity = DamageType.Drill;
    public bool shieldActive;
}
public static class Map
{
    public static List<Unit> units;
    public static int groundLayer = 7;
}
public static class GameModeController
{
    public static bool DoesPlayerNumDamage(int source, int target) { return source != target; }
}
namespace Rogueforce
{
    public static class ValueOrchestrator
    {
        public static int multiplier = 1;
        public static int GetModifiedDamage(int damage, int playerNum) { return damage * multiplier; }
    }
}
public class HitRequest
{
    public MonoBehaviour sender;
    public GameObject receiver;
    public int damage;
    public DamageType type;
    public float xForce;
}
public static class MapController
{
    public static List<HitRequest> hits = new List<HitRequest>();
    public static void Damage_Networked(MonoBehaviour sender, GameObject receiver, int damage,
        DamageType type, float xForce, float yForce, float x, float y)
    {
        hits.Add(new HitRequest { sender = sender, receiver = receiver, damage = damage, type = type, xForce = xForce });
        DamageRelay relay = receiver.relay;
        if (!relay.shieldActive && relay.immunity != type) relay.unit.health -= damage;
    }
}
