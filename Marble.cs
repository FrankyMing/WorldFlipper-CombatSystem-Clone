using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class Marble : MonoBehaviour, IRespawnable
{
    public enum BallState { Idle, Active, Bouncing}
    private BallState _currentState = BallState.Idle;
    public BallState CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState == value) return;
            _currentState = value;
            OnStateChanged(_currentState); // 狀態切換時觸發封裝好的邏輯
        }
    }
    public bool CanAction => GameManager.Instance.CurrentState == GameState.Playing && CurrentState == BallState.Active;
    private Transform ClosetEnemyPos;
    private Rigidbody2D rigi;
    public float maxSpeed = 25f;
    public GameObject Arrow;
    public float Limit_Timer_Trick = 5f;
    private bool isTargetInRange = false;
    [Header("動態效果")]
    public Animator Effeft;
    public GameObject Effect_OBJ;
    [Header("預測落點")]
    public GameObject crosshair;
    public GameObject Line;
    private Vector2 currentPredictDir; // 新增：儲存當前的預測方向
    private float dashCooldown = 2.0f;
    private float dashTimer = 0f;
    public bool CanDash => dashTimer <= 0;//表達式主體屬性
    private Vector3 currentDashDirection;
    private float currentDashAngle;
    private Vector3 lastSafePosition;
    [Header("尖刺反彈曲線")]
    public AnimationCurve jumpCurve;
    private CharacterSlotManager slotManager;
    [Header("拖尾效果")]
    public float speedthreshold = 10.0f;
    private ParticleSystem PS;
    private ParticleSystem.EmissionModule emission;
    [Header("外部控制狀態")]
    public bool IsSkillOverride = false;

    void Start()
    {
        rigi = GetComponent<Rigidbody2D>();
        slotManager = GetComponent<CharacterSlotManager>();
        PS = GetComponent<ParticleSystem>();
        Arrow.SetActive(false);
        crosshair.SetActive(false); // 開始時隱藏準心
        Line.SetActive(false);
        emission = PS.emission;
        lastSafePosition = transform.position;
        OnStateChanged(_currentState);
        //開場狀態
    }

    void Update()
    {
        if (CurrentState == BallState.Idle)
        {
            UpdateWaitingMovement();
        }
        if (!CanAction) return;
        //二段衝刺
        if (dashTimer > 0) dashTimer -= Time.deltaTime;
        HandleDashInput();
        // 粒子拖尾由速度決定
        if (CurrentState == BallState.Active)
        {
            emission.enabled = rigi.velocity.magnitude > speedthreshold;
        }
        else
        {
            emission.enabled = false;
        }
    }
    void FixedUpdate()
    {
        if (IsSkillOverride) return;
        // 如果當前速度超過了我們允許的極速
        if (rigi.velocity.magnitude > maxSpeed)
        {
            // 強制把速度鎖定在最高速，方向保持不變
            rigi.velocity = Vector2.ClampMagnitude(rigi.velocity, maxSpeed);
        }
    }
    // 訂閱
    private void OnEnable()
    {
        GameManager.OnGameStateChangedEvent += HandleGameFlow;
        PlayerControl.OnScreenTapped += TryExecuteDash;
    }
    // 取消訂閱，避免記憶體洩漏
    private void OnDisable()
    {
        GameManager.OnGameStateChangedEvent -= HandleGameFlow;
        PlayerControl.OnScreenTapped -= TryExecuteDash;
    }
    //接收全域廣播
    private void HandleGameFlow(GameState globalState)
    {
        switch (globalState)
        {
            case GameState.Playing:
                // 只有當球目前不是死的，才切換到 Active
                if(CurrentState == BallState.Idle)
                {
                    CurrentState = BallState.Active;
                }
                break;

            case GameState.GameOver:
            case GameState.GameWin:
                rigi.isKinematic = true;
                rigi.velocity = Vector3.zero;
                rigi.angularVelocity = 0;
                rigi.gravityScale = 0;
                break;
        }
    }
    //行為邏輯中心
    private void OnStateChanged(BallState newState)
    {
        switch (newState)
        {
            case BallState.Idle:
                rigi.isKinematic = true;
                rigi.gravityScale = 0;
                break;

            case BallState.Active:
                // 恢復所有功能
                slotManager.NotifyAllMembersStart();
                rigi.gravityScale = 0.8f;
                rigi.isKinematic = false;
                rigi.drag = 0;
                break;

            case BallState.Bouncing:
                // 立即停止所有物理運動
                rigi.velocity = Vector2.zero;
                rigi.isKinematic = true;
                // 參數：(目標座標, 跳躍高度, 跳躍次數, 持續秒數)
                transform.DOJump(lastSafePosition, 4f, 1, 0.8f)
                         .SetEase(jumpCurve) // 這是靈魂，它會完全覆蓋預設的加減速
                         .OnComplete(() => {
                             rigi.isKinematic = false;
                             rigi.velocity = new Vector2(0, -9f);
                             CurrentState = BallState.Active;
                         });
                break;
        }
    }
    
    //跳板偵測
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Board"))
        {
            Vector3 BoardPos = collision.collider.bounds.center;
            float targetX = BoardPos.x;
            float targetY = BoardPos.y + 2.5f;

            lastSafePosition = new Vector3(targetX, targetY, 0);
        }
    }
    public void SetPredictDirection(Vector2 dir)
    {
        currentPredictDir = dir;
        Line.SetActive(true);
        Line.transform.up = dir;//
    }
    public void HidePredictLine()
    {
        currentPredictDir = Vector2.zero; // 清除預測
        Line.SetActive(false);
    }
    //擊發、切換、藏線
    public void Launch(float power)
    {
        if (currentPredictDir == Vector2.zero) return;
        rigi.velocity = currentPredictDir * power;
        HidePredictLine();
        StopAllCoroutines();
        StartCoroutine(LaunchSequence());
    }

    private IEnumerator LaunchSequence()
    {
        int originalLayer = gameObject.layer;
        gameObject.layer = LayerMask.NameToLayer("Ball_Ghost");

        yield return new WaitForSeconds(0.2f);

        gameObject.layer = originalLayer;
    }
    public void UpdateWaitingMovement()
    {
        rigi.velocity = Vector2.zero;
        rigi.gravityScale = 0;

        // 使用 Sin 函數實現平滑晃動
        // Time.time * moveSpeed 決定快慢
        // 乘上 moveRange 決定左右移動多遠
        float xOffset = Mathf.Sin(Time.time * 1.5f) * 1f;

        transform.position = new Vector2(0 + xOffset, 4.2f);
    }
    public void SetDashTarget(Transform target)
    {
        // 儲存目標
        ClosetEnemyPos = target;
        // 如果 target 不是 null，代表「在範圍內」；如果是 null，代表「不在範圍內」
        isTargetInRange = (target != null);
    }
    private void HandleDashInput()
    {
        // 如果目標在過程中被銷毀了（變成了 null）
        if (isTargetInRange && ClosetEnemyPos == null)
        {
            SetDashTarget(null); // 強制關閉鎖定狀態
            return;
        }
        // 只有在「遊戲中」、且「有鎖定目標」、且「不在冷卻中」時才執行
        if (CurrentState == BallState.Active && isTargetInRange && CanDash)
        {
            if (ClosetEnemyPos == null) return;
            currentDashDirection = (ClosetEnemyPos.position - transform.position).normalized;
            currentDashAngle = Mathf.Atan2(currentDashDirection.y, currentDashDirection.x) * Mathf.Rad2Deg - 90f;
            Arrow.transform.rotation = Quaternion.Euler(new Vector3(0, 0, currentDashAngle));
            if (!Arrow.activeSelf) Arrow.SetActive(true);
        }
        else
        {
            // 關鍵：如果條件不滿足（冷卻中或不在範圍），一定要關閉箭頭
            if (Arrow.activeSelf) Arrow.SetActive(false);
        }
    }
    private void TryExecuteDash()
    {
        if (CurrentState == BallState.Active && isTargetInRange && CanDash && ClosetEnemyPos != null)
        {
            ExecuteDash();
        }
    }
    private void ExecuteDash()
    {
        rigi.velocity = Vector2.zero;
        rigi.AddForce(currentDashDirection * 12, ForceMode2D.Impulse);
        Arrow.SetActive(false);
        Effect_OBJ.transform.localPosition = gameObject.transform.localPosition;
        Effect_OBJ.transform.rotation = Quaternion.Euler(new Vector3(0, 0, currentDashAngle));
        Effeft.SetTrigger("IsSprint");
        // 重置冷卻計時器
        dashTimer = dashCooldown;
    }
    public void Respawn()
    {
        CurrentState = BallState.Bouncing;
    }
}

