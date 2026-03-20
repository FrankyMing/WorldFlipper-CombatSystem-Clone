using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public class SkillEnergySystem : MonoBehaviour
{
    [Header("技能條設定")]
    public float maxEnergy = 250f;          // 原本的300f
    public float moveStepUnit = 12.5f;     // 原本的20f(每移動多少距離算一階)
    public float hitEnergyBonus = 5f;      // 每次碰撞額外加多少能量
    public float skillCooldown = 5f;       // 大招冷卻時間

    [Header("UI表現")]
    public SkillGaugeUI myUIBar; // 自己拉對應的 UI (P1 就拉 P1 的 UI)

    [Header("當前狀態")]
    public float currentEnergy = 0f;
    public bool isOnCooldown = false;
    private float cooldownTimer = 0f;
    private Vector3 lastPos;
    private float accumulatedDistance = 0f;

    [Header("大招演出")]
    public PlayableDirector skillDirector;
    [Header("角色立繪")]
    public Sprite characterPortrait;

    private bool isGameActive = false;

    public static bool GlobalSkillLock = false;
    private bool isMySkillPlaying = false;

    void OnEnable()
    {
        GameManager.OnGameStateChangedEvent += HandleGameStateChanged;
        if (skillDirector != null)
        {
            skillDirector.stopped += OnTimelineFinished;
        }

        GlobalSkillLock = false;
        isMySkillPlaying = false;
    }

    void OnDisable()
    {
        GameManager.OnGameStateChangedEvent -= HandleGameStateChanged;
        if (skillDirector != null)
        {
            skillDirector.stopped -= OnTimelineFinished;
        }
    }

    // 當 GameManager 廣播狀態改變時，這個方法會被自動呼叫
    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Playing)
        {
            if (!isGameActive)
            {
                isGameActive = true;
                lastPos = transform.position; // 進入 Playing 瞬間重置起點
            }
        }
        else
        {
            // 如果切換到 Stop, GameOver, Prepare 等狀態，一律暫停充能
            isGameActive = false;
        }
    }
    private void OnTimelineFinished(PlayableDirector director)
    {
        if (isMySkillPlaying)
        {
            GlobalSkillLock = false;
            isMySkillPlaying = false;
            // 把物理重力跟控制權還給玩家
            SkillController sc = GetComponent<SkillController>();
            if (sc != null) sc.ReleaseSkillOverride();
        }
    }

    void Start()
    {
        UpdateUIDisplay();
    }

    void Update()
    {
        if (!isGameActive) return;
        // 如果這顆球正在播大招
        if (isMySkillPlaying)
        {
            Marble marble = GetComponent<Marble>();
            // 但角色突然被打死了
            if (marble != null && !marble.CanAction)
            {
                if (skillDirector != null) skillDirector.Stop();
                GlobalSkillLock = false;
                isMySkillPlaying = false;

                SkillController sc = GetComponent<SkillController>();
                if (sc != null) sc.ReleaseSkillOverride();
            }
        }

        HandleCooldown();
        HandleMoveEnergy();
    }

    // --- 移動充能邏輯 (轉移並優化你的 TotalDistance) ---
    private void HandleMoveEnergy()
    {
        if (isOnCooldown || currentEnergy >= maxEnergy) return;

        // 計算位移
        float frameDistance = Vector3.Distance(transform.position, lastPos);
        accumulatedDistance += frameDistance;
        lastPos = transform.position;

        // 依照你的舊邏輯：每達到一次 step 單位才增加
        if (accumulatedDistance >= moveStepUnit)
        {
            float stepsCount = Mathf.Floor(accumulatedDistance / moveStepUnit);
            AddEnergy(stepsCount * moveStepUnit);
            accumulatedDistance %= moveStepUnit; // 留下餘數，下次繼續累加
        }
    }

    // --- 通用充能入口 (供移動、碰撞、或其他技能呼叫) ---
    public void AddEnergy(float amount)
    {
        if (isOnCooldown) return;

        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0, maxEnergy);
        UpdateUIDisplay();
    }

    // --- 大招發動邏輯 ---
    public bool TryActivateSkill()
    {
        if (GlobalSkillLock) return false;//防搶斷機制
        Marble marble = GetComponent<Marble>();
        if (marble != null && !marble.CanAction) return false;//大招發動前檢查
        if (!isOnCooldown && currentEnergy >= maxEnergy)
        {
            if (SkillManager.Instance != null)
            {
                GlobalSkillLock = true;
                isMySkillPlaying = true;
                
                SkillManager.Instance.CastUltimate(characterPortrait, skillDirector);
            }
            // 進入冷卻與重置
            ResetEnergy();
            return true;
        }
        return false;
    }

    private void HandleCooldown()
    {
        if (isOnCooldown)
        {
            cooldownTimer += Time.deltaTime;
            if (cooldownTimer >= skillCooldown)
            {
                isOnCooldown = false;
                cooldownTimer = 0;
            }
        }
    }

    private void ResetEnergy()
    {
        currentEnergy = 0;
        accumulatedDistance = 0;
        isOnCooldown = true;
        UpdateUIDisplay();
    }
    private void UpdateUIDisplay()
    {
        if (myUIBar != null)
        {
            float percent = currentEnergy / maxEnergy;
            myUIBar.UpdateGauge(percent);
        }
    }
}
