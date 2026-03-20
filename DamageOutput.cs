using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageOutput : MonoBehaviour
{
    [Header("傷害參數")]
    public DamageType AttackType;
    public float baseDamage = 10f;
    public float damageMultiplier = 1f;

    [Header("目標判定")]
    [Tooltip("下拉打勾：這招可以打中哪些 Layer？ (例如 Boss, Enemy)")]
    public LayerMask targetLayers;

    [Tooltip("這招是持續傷害嗎？(如雷射 Beam)")]
    public bool isContinuousDamage = false;
    public float damageTickRate = 0.5f;

    // --- 內部記憶體 (防重複扣血與效能暴衝) ---
    private Dictionary<IDamageable, float> nextDamageTime = new Dictionary<IDamageable, float>();
    private float singleHitCooldown = 0.1f; // 0.1秒極短無敵幀，專治多重 Collider

    // --- 單次攻擊判定 ---
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isContinuousDamage) return;
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null) return;
        // 0.1 秒內完美吸收 Collider A 跟 Collider B 的雙重衝擊
        if (nextDamageTime.ContainsKey(target) && Time.time < nextDamageTime[target]) return;

        ExecuteDamage(other, target);

        // 紀錄時間
        nextDamageTime[target] = Time.time + singleHitCooldown;
    }

    // --- 持續攻擊判定 (雷射等) ---
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isContinuousDamage) return;
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null) return;

        // 持續傷害還是需要計時器 (例如每 0.5 秒跳一次傷害)，不然每秒會扣 60 次血
        if (!nextDamageTime.ContainsKey(target) || Time.time >= nextDamageTime[target])
        {
            ExecuteDamage(other, target);
            nextDamageTime[target] = Time.time + damageTickRate;
        }
    }

    // --- 核心發送傷害與回收資源 (倍率/能量都在這) ---
    private void ExecuteDamage(Collider2D victimCollider, IDamageable target)
    {
        GameObject victimObj = victimCollider.gameObject;

        // 計算基本數值
        int finalDmg = Mathf.RoundToInt(baseDamage * damageMultiplier);
        Vector2 hitPoint = victimCollider.bounds.ClosestPoint(transform.position);
        Vector2 hitDirection = ((Vector2)victimObj.transform.position - hitPoint).normalized;

        // Boss 的特例處理 (連擊、倍率重置、能量回收、彈射偏移)
        if (victimObj.CompareTag("Boss"))
        {
            hitDirection = Quaternion.Euler(0, 0, Random.Range(-15, 15)) * hitDirection;

            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.AddCombo(1);
            }

            if (damageMultiplier == 4f)
            {
                damageMultiplier = 1f;
            }

            SkillEnergySystem energySystem = GetComponentInParent<SkillEnergySystem>();
            if (energySystem != null)
            {
                energySystem.AddEnergy(energySystem.hitEnergyBonus);
            }
        }

        // 打包資料並送出傷害
        DamageData data = new DamageData
        {
            amount = finalDmg,
            hitPoint = hitPoint,
            hitDirection = hitDirection,
            type = this.AttackType,
            attacker = this.gameObject
        };

        target.TakeDamage(data);
    }

    private void Explode()
    {
        Destroy(gameObject);
    }
}
