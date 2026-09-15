using System.Collections.Generic;
using Rogueforce;
using UnityEngine;

namespace Aquabro
{
    internal static class RisingWaveDamage
    {
        // 与 FlameWallExplosion.CreateFlashBangPoint 的单位命中半径一致。
        internal const float HitRadius = 12f;

        internal static void HitRelays(MonoBehaviour firedBy, int playerNum, int damage,
            DamageType damageType, float x, float y, float xForce)
        {
            if (firedBy == null || damage <= 0) return;

            Collider[] colliders = Physics.OverlapSphere(new Vector3(x, y, 0f),
                HitRadius, Map.groundLayer);
            HashSet<DamageRelay> hitRelays = new HashSet<DamageRelay>();
            foreach (Collider collider in colliders)
            {
                DamageRelay relay = collider.GetComponent<DamageRelay>();
                Unit target = relay == null ? null : relay.unit;
                if (target == null || target == firedBy || target.health <= 0 ||
                    target.invulnerable ||
                    !GameModeController.DoesPlayerNumDamage(playerNum, target.playerNum))
                    continue;

                // 普通单位已由同一水墙节点的 Map.HitUnits 结算。
                // 大型 Boss 的部件可能远离主体坐标，需要保留部件命中。
                if (Map.units != null && Map.units.Contains(target) &&
                    Mathf.Abs(target.X - x) - HitRadius < target.width &&
                    Mathf.Abs(target.Y + target.height / 2f + 4f - y) - HitRadius < target.height)
                    continue;
                if (!hitRelays.Add(relay)) continue;

                // 交给原版部件处理护甲、伤害类型转换和每帧去重，不能直接扣主体血量。
                MapController.Damage_Networked(firedBy, collider.gameObject,
                    ValueOrchestrator.GetModifiedDamage(damage, playerNum), damageType,
                    xForce, 0f, x, y);
            }
        }
    }
}
