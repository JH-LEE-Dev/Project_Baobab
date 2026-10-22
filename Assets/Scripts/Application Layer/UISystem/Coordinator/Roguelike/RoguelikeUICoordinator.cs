using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 시스템과 로그라이크 UIView 사이의 연결. GameplayUICoordinator와 같은 역할 분담을 따른다.
///   시스템 → UI: SignalHub 시그널을 받아 뷰 메서드를 부른다. 데이터는 IRoguelikeRunInfo에서 읽어 ViewData로 바꾼다.
///   UI → 시스템: 뷰의 이벤트를 받아 요청 시그널로 다시 발행한다.
///
/// 뷰가 비어 있어도(프리팹 미등록) 흐름이 멈추지 않는다. 기다려야 하는 지점(카드 연출 완료, 결과 확인)은
/// 뷰가 없으면 곧바로 완료 시그널을 보낸다.
/// </summary>
public sealed class RoguelikeUICoordinator
{
    private SignalHub signalHub;
    private IRoguelikeRunInfo runInfo;

    private UIView_RoguelikeHUD hudUI;
    private UIView_RoguelikeCardSelect cardSelectUI;
    private UIView_RoguelikeOwnedCards ownedCardsUI;
    private UIView_RoguelikeResult resultUI;
    private UIView_RoguelikeRecords recordsUI;
    private UIView_ESC escUI;
    // 본편 하단 팝업(코인 HUD·가방). 로그라이크에는 골드도 인벤토리도 없어 둘 다 숨긴다(기획서 v2.4 10-1).
    private UIView_Popup mainPopupUI;
    // 본편 HUD. 도끼 내구도 HUD(장비 HUD)를 숨기는 데만 쓴다(기획서 v2.2 10-1).
    private UIView_HUD mainHudUI;

    private readonly List<RoguelikeCardViewData> cardViewBuffer = new List<RoguelikeCardViewData>(8);
    private readonly List<RoguelikeOwnedCardViewData> ownedViewBuffer = new List<RoguelikeOwnedCardViewData>(32);
    private readonly List<RoguelikeRecordViewData> clearRankingBuffer = new List<RoguelikeRecordViewData>(5);
    private readonly List<RoguelikeRecordViewData> unlimitRankingBuffer = new List<RoguelikeRecordViewData>(5);

    private Action cardPresentationDoneHandler;
    private Action ownedCardsHiddenHandler;

    // 보유 카드 창이 건 일시정지(기획서 5-2 "전투 중에도 확인" → 여는 동안 정지). 열기 직전 timeScale을 기억했다가
    // 닫힐 때 되돌린다. ESC 메뉴는 닫을 때 timeScale을 1로 되돌리므로, 창이 열린 채 ESC 메뉴가 닫히면 Tick이 다시 건다.
    private bool bOwnedCardsPaused;
    private float timeScaleBeforeOwnedCards = 1f;

    // HUD 임계치 바는 매 프레임 같은 값을 넣지 않도록 마지막 값을 기억한다.
    private int lastTreeCount = -1;
    private int lastThreshold = -1;
    private EPressureState lastPressureState;
    // 스태미너 바·붉은 화면 경고도 바뀔 때만 HUD에 넣는다(임계점을 넘었는지 / 한 번 맞으면 죽는지).
    private bool bLastPressureExceeded;
    private bool bLastPressureLethal;
    private float lastGraceRatio = -1f;
    private float lastCooldownRatio = -1f;

    public void Initialize(SignalHub _signalHub, IRoguelikeRunInfo _runInfo, UIView_RoguelikeHUD _hudUI, UIView_RoguelikeCardSelect _cardSelectUI,
        UIView_RoguelikeOwnedCards _ownedCardsUI, UIView_RoguelikeResult _resultUI, UIView_RoguelikeRecords _recordsUI, UIView_ESC _escUI,
        UIView_Popup _mainPopupUI, UIView_HUD _mainHudUI)
    {
        // 로그라이크는 아이템 드롭·인벤토리(기획서 v2.2 3-1)도 골드(v2.4)도 쓰지 않는다. 가방 HUD와 코인 HUD를 모두 숨긴다.
        mainPopupUI = _mainPopupUI;
        mainPopupUI?.SetInventoryVisibleForRun(false);
        mainPopupUI?.SetCoinVisibleForRun(false);

        // 도끼 내구도는 무제한이라 내구도 HUD를 숨긴다(기획서 v2.2 3-1, 10-1).
        mainHudUI = _mainHudUI;
        mainHudUI?.SetEquipmentHiddenForRun(true);
        mainHudUI?.SetBuffHiddenForRun(true);

        signalHub = _signalHub;
        runInfo = _runInfo;
        hudUI = _hudUI;
        cardSelectUI = _cardSelectUI;
        ownedCardsUI = _ownedCardsUI;
        resultUI = _resultUI;
        recordsUI = _recordsUI;
        escUI = _escUI;

        hudUI?.SetGoldVisible(false);

        cardPresentationDoneHandler = OnCardPresentationDone;
        ownedCardsHiddenHandler = OnOwnedCardsHidden;

        SubscribeSignals();
        BindEvents();
    }

    public void Release()
    {
        UnSubscribeSignals();
        ReleaseEvents();
        escUI?.SetLobbyMode(false);
        mainPopupUI?.SetInventoryVisibleForRun(true);
        mainPopupUI?.SetCoinVisibleForRun(true);
        mainHudUI?.SetEquipmentHiddenForRun(false);
        mainHudUI?.SetBuffHiddenForRun(false);
        ResumeFromOwnedCards();
    }

    /// <summary>
    /// 본편 HUD·하단 팝업의 맵 전환 내림·올림 모션이 재생 중인지. 로비 진입 암전은 이 모션이 끝난 뒤 걷힌다 -
    /// 그 전에 걷히면 HUD가 내려가는 모습이 페이드 아웃 사이로 보인다.
    /// </summary>
    public bool IsMainHudMotionPlaying =>
        (null != mainHudUI && mainHudUI.IsMapTransitionMotionPlayingForRun) || (null != mainPopupUI && mainPopupUI.IsMapTransitionMotionPlayingForRun);

    /// <summary>카드 연출 완료를 끝내 받지 못하고 다음 단계로 넘어갈 때. 남아 있는 카드 선택 화면을 닫는다.</summary>
    public void ForceCloseCardSelection()
    {
        if (null != cardSelectUI && cardSelectUI.IsVisible) cardSelectUI.Hide();
    }

    /// <summary>매 프레임. 바뀐 값만 HUD에 넣는다.</summary>
    public void Tick()
    {
        // 보유 카드 창이 열려 있는 동안은 계속 멈춰 있어야 한다. ESC 메뉴가 그 위에서 열렸다 닫히며 1로 돌려놓은 경우를 되잡는다.
        if (bOwnedCardsPaused && 0f != UnityEngine.Time.timeScale && (null == escUI || false == escUI.IsVisible))
        {
            UnityEngine.Time.timeScale = 0f;
        }

        if (null == hudUI || null == runInfo) return;

        int treeCount = runInfo.FieldTreeCount;
        int threshold = runInfo.Threshold;
        EPressureState state = runInfo.PressureState;

        // 임계점 경고: 넘었으면 스태미너 바 + 붉은 화면, 아직 안 넘었어도 한 번 맞으면 죽는 상태면 스태미너 바만. 스태미너가 바뀔 때마다 달라질 수 있어 임계치 값 비교보다 먼저 본다.
        bool bExceeded = EPressureState.Safe != state;
        bool bLethal = runInfo.PressureHitLethal;
        if (bExceeded != bLastPressureExceeded || bLethal != bLastPressureLethal)
        {
            bLastPressureExceeded = bExceeded;
            bLastPressureLethal = bLethal;
            if (null != mainHudUI) mainHudUI.SetRoguelikePressureWarning(bExceeded || bLethal, bExceeded);
        }
        float grace = runInfo.PressureGraceRatio;
        float cooldown = runInfo.PressureCooldownRatio;

        if (treeCount == lastTreeCount && threshold == lastThreshold && state == lastPressureState
            && Math.Abs(grace - lastGraceRatio) < 0.005f && Math.Abs(cooldown - lastCooldownRatio) < 0.005f)
        {
            return;
        }

        lastTreeCount = treeCount;
        lastThreshold = threshold;
        lastPressureState = state;
        lastGraceRatio = grace;
        lastCooldownRatio = cooldown;

        hudUI.SetThreshold(treeCount, threshold, runInfo.SpawnCap, state, grace, cooldown);
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<RoguelikePhaseChangedSignal>(PhaseChanged);
        signalHub.Subscribe<RoguelikeWaveStartedSignal>(WaveStarted);
        signalHub.Subscribe<RoguelikeWaveProgressSignal>(WaveProgress);
        signalHub.Subscribe<RoguelikePressureDamagedSignal>(PressureDamaged);
        signalHub.Subscribe<RoguelikeCardSelectionOpenedSignal>(CardSelectionOpened);
        signalHub.Subscribe<RoguelikeOffersChangedSignal>(OffersChanged);
        signalHub.Subscribe<RoguelikeCardPurchasedSignal>(CardPurchased);
        signalHub.Subscribe<RoguelikeCardSelectionSkippedSignal>(CardSelectionSkipped);
        signalHub.Subscribe<RoguelikeCardShopRejectedSignal>(CardShopRejected);
        signalHub.Subscribe<RoguelikeResultReadySignal>(ResultReady);
        signalHub.Subscribe<RoguelikeRecordsRequestedSignal>(RecordsRequested);
        signalHub.Subscribe<RoguelikeRunEndedSignal>(RunEnded);
        signalHub.Subscribe<RoguelikeEntryScreenCoveredSignal>(EntryScreenCovered);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<RoguelikePhaseChangedSignal>(PhaseChanged);
        signalHub.UnSubscribe<RoguelikeWaveStartedSignal>(WaveStarted);
        signalHub.UnSubscribe<RoguelikeWaveProgressSignal>(WaveProgress);
        signalHub.UnSubscribe<RoguelikePressureDamagedSignal>(PressureDamaged);
        signalHub.UnSubscribe<RoguelikeCardSelectionOpenedSignal>(CardSelectionOpened);
        signalHub.UnSubscribe<RoguelikeOffersChangedSignal>(OffersChanged);
        signalHub.UnSubscribe<RoguelikeCardPurchasedSignal>(CardPurchased);
        signalHub.UnSubscribe<RoguelikeCardSelectionSkippedSignal>(CardSelectionSkipped);
        signalHub.UnSubscribe<RoguelikeCardShopRejectedSignal>(CardShopRejected);
        signalHub.UnSubscribe<RoguelikeResultReadySignal>(ResultReady);
        signalHub.UnSubscribe<RoguelikeRecordsRequestedSignal>(RecordsRequested);
        signalHub.UnSubscribe<RoguelikeRunEndedSignal>(RunEnded);
        signalHub.UnSubscribe<RoguelikeEntryScreenCoveredSignal>(EntryScreenCovered);
    }

    private void BindEvents()
    {
        if (null != hudUI)
        {
            hudUI.OwnedCardsRequestedEvent -= OwnedCardsRequested;
            hudUI.OwnedCardsRequestedEvent += OwnedCardsRequested;
        }

        if (null != cardSelectUI)
        {
            cardSelectUI.BuyRequestedEvent -= BuyRequested;
            cardSelectUI.BuyRequestedEvent += BuyRequested;
            cardSelectUI.RerollRequestedEvent -= RerollRequested;
            cardSelectUI.RerollRequestedEvent += RerollRequested;
        }

        if (null != resultUI)
        {
            resultUI.ConfirmRequestedEvent -= ResultConfirmRequested;
            resultUI.ConfirmRequestedEvent += ResultConfirmRequested;
        }

        if (null != recordsUI)
        {
            recordsUI.ClosedEvent -= RecordsClosed;
            recordsUI.ClosedEvent += RecordsClosed;
        }

        if (null != ownedCardsUI)
        {
            ownedCardsUI.HiddenEvent -= ownedCardsHiddenHandler;
            ownedCardsUI.HiddenEvent += ownedCardsHiddenHandler;
        }
    }

    private void ReleaseEvents()
    {
        if (null != hudUI) hudUI.OwnedCardsRequestedEvent -= OwnedCardsRequested;

        if (null != cardSelectUI)
        {
            cardSelectUI.BuyRequestedEvent -= BuyRequested;
            cardSelectUI.RerollRequestedEvent -= RerollRequested;
        }

        if (null != resultUI) resultUI.ConfirmRequestedEvent -= ResultConfirmRequested;
        if (null != recordsUI) recordsUI.ClosedEvent -= RecordsClosed;
        if (null != ownedCardsUI) ownedCardsUI.HiddenEvent -= ownedCardsHiddenHandler;
    }

    // // 시스템 → UI

    private void PhaseChanged(RoguelikePhaseChangedSignal _signal)
    {
        bool bCombatPhase = ERunPhase.Lobby != _signal.phase && ERunPhase.ReturningToLobby != _signal.phase && ERunPhase.None != _signal.phase;

        // 보유 카드를 열 수 없는 단계(쓰러짐·결과·로비 쪽)로 넘어가면 열려 있던 창과 일시정지를 걷는다.
        if (false == CanOpenOwnedCards(_signal.phase)) CloseOwnedCards();

        // 런 도중 ESC의 메인메뉴 버튼은 "로비"(포기)다. 로비에서는 진짜 메인메뉴로 나가므로 본래 문구로 둔다(기획서 3-7).
        escUI?.SetLobbyMode(bCombatPhase);

        // 전투 단계를 벗어나면(결과·로비 등) 임계점 경고(스태미너 바 + 붉은 화면)를 확실히 끈다.
        if (false == bCombatPhase && (bLastPressureExceeded || bLastPressureLethal))
        {
            bLastPressureExceeded = false;
            bLastPressureLethal = false;
            if (null != mainHudUI) mainHudUI.SetRoguelikePressureWarning(false, false);
        }

        if (null == hudUI) return;
        hudUI.SetCombatVisible(bCombatPhase);
    }

    private void WaveStarted(RoguelikeWaveStartedSignal _signal)
    {
        if (null == hudUI) return;

        hudUI.SetWave(_signal.spec.waveNumber, _signal.spec.bUnlimit, _signal.spec.unlimitIndex);
        hudUI.SetWaveProgress(0, _signal.spec.killTarget);
    }

    private void WaveProgress(RoguelikeWaveProgressSignal _signal)
    {
        hudUI?.SetWaveProgress(_signal.killed, _signal.target);
    }

    private void PressureDamaged(RoguelikePressureDamagedSignal _signal)
    {
        hudUI?.PlayPressureDamaged(_signal.damage);
    }

    private void CardSelectionOpened(RoguelikeCardSelectionOpenedSignal _signal)
    {
        if (null == cardSelectUI)
        {
            ResolveSelectionWithoutView();
            return;
        }

        BuildCardViewData();
        cardSelectUI.OpenSelection(cardViewBuffer, runInfo.CanReroll);
    }

    /// <summary>
    /// 카드 선택 화면이 없으면(프리팹 미등록) 고를 수단이 없어 런이 이 단계에서 멈춘다. 결과·순위표 화면이 없을 때처럼
    /// 흐름을 이어 준다: 건너뛰기가 없으므로(기획서 v2.4 4-1) 제시된 첫 카드를 고른다.
    /// </summary>
    private void ResolveSelectionWithoutView()
    {
        UnityEngine.Debug.LogWarning("[RoguelikeUICoordinator] UIView_RoguelikeCardSelect가 없어 카드 선택을 자동으로 처리합니다. GameplayUIManager의 viewPrefabs를 확인하십시오.");

        IReadOnlyList<CardOffer> offers = runInfo.CurrentOffers;
        for (int i = 0; i < offers.Count; i++)
        {
            if (false == offers[i].IsValid) continue;

            signalHub.Publish(new RoguelikeCardBuyRequestedSignal(i));
            return;
        }

        UnityEngine.Debug.LogError("[RoguelikeUICoordinator] 카드 선택이 열렸는데 고를 수 있는 카드가 없습니다. 런이 카드 선택에서 멈춥니다.");
    }

    private void OffersChanged(RoguelikeOffersChangedSignal _signal)
    {
        if (null == cardSelectUI) return;

        BuildCardViewData();
        cardSelectUI.ReplaceOffers(cardViewBuffer, runInfo.CanReroll);
    }

    private void CardPurchased(RoguelikeCardPurchasedSignal _signal)
    {
        if (null == cardSelectUI || false == cardSelectUI.IsVisible)
        {
            OnCardPresentationDone();
            return;
        }

        RoguelikeCardPurchaseViewData purchase = new RoguelikeCardPurchaseViewData
        {
            offerIndex = _signal.result.offerIndex,
            newLevel = _signal.result.newLevel,
            bNewCard = _signal.result.bNewCard,
            displayGrade = _signal.result.displayGrade,
        };

        cardSelectUI.PlayPurchase(in purchase, runInfo.LastDiscardedOfferIndices, cardPresentationDoneHandler);
    }

    private void CardSelectionSkipped(RoguelikeCardSelectionSkippedSignal _signal)
    {
        if (null == cardSelectUI || false == cardSelectUI.IsVisible)
        {
            OnCardPresentationDone();
            return;
        }

        cardSelectUI.CloseSelection(cardPresentationDoneHandler);
    }

    private void CardShopRejected(RoguelikeCardShopRejectedSignal _signal)
    {
        cardSelectUI?.PlayRejected(_signal.reason, _signal.offerIndex);
    }

    private void ResultReady(RoguelikeResultReadySignal _signal)
    {
        if (null == resultUI)
        {
            // 결과 화면이 없으면 기다릴 대상이 없다. 바로 확인으로 처리해 로비로 넘긴다.
            signalHub.Publish(new RoguelikeResultConfirmedSignal());
            return;
        }

        RunRecordEntry entry = runInfo.LastRunRecord;
        List<RoguelikeOwnedCardViewData> cards = new List<RoguelikeOwnedCardViewData>(null != entry ? entry.cards.Count : 0);
        if (null != entry) FillRecordCards(entry, cards);

        RoguelikeResultViewData data = new RoguelikeResultViewData
        {
            reachedWave = null != entry ? entry.reachedWave : 0,
            bUnlimit = null != entry && entry.bCleared,
            unlimitWave = null != entry ? entry.reachedUnlimitWave : 0,
            bAbandoned = null != entry && entry.bAbandoned,
            playTimeSeconds = null != entry ? entry.playTimeSeconds : 0f,
            treesFelled = null != entry ? entry.treesFelled : 0,
            cards = cards,
            clearRank = runInfo.LastClearRank,
            unlimitRank = runInfo.LastUnlimitRank,
        };

        resultUI.OpenResult(in data);
    }

    private void RecordsRequested(RoguelikeRecordsRequestedSignal _signal)
    {
        if (null == recordsUI)
        {
            signalHub.Publish(new RoguelikeRecordsClosedSignal());
            return;
        }

        RoguelikeRecordData records = runInfo.Records;
        RoguelikeRecordViewData recent = default;
        if (null != records && records.bHasRecent) recent = ToRecordView(records.recent);

        clearRankingBuffer.Clear();
        unlimitRankingBuffer.Clear();

        if (null != records)
        {
            for (int i = 0; i < records.clearRanking.Count; i++) clearRankingBuffer.Add(ToRecordView(records.clearRanking[i]));
            for (int i = 0; i < records.unlimitRanking.Count; i++) unlimitRankingBuffer.Add(ToRecordView(records.unlimitRanking[i]));
        }

        recordsUI.OpenRecords(in recent, clearRankingBuffer, unlimitRankingBuffer);
    }

    // // UI → 시스템

    private void BuyRequested(int _offerIndex) => signalHub.Publish(new RoguelikeCardBuyRequestedSignal(_offerIndex));
    private void RerollRequested() => signalHub.Publish(new RoguelikeCardRerollRequestedSignal());
    private void OnCardPresentationDone() => signalHub.Publish(new RoguelikeCardSelectionPresentationDoneSignal());

    private void ResultConfirmRequested()
    {
        // 결과 화면은 여기서 닫지 않는다. 확인 직후 결과 화면 위에 디스토션이 걸리고(기획서 4-1 · 3-7), 암전으로 덮인 뒤 닫는다(EntryScreenCovered).
        signalHub.Publish(new RoguelikeResultConfirmedSignal());
    }

    private void RecordsClosed() => signalHub.Publish(new RoguelikeRecordsClosedSignal());

    private void OwnedCardsRequested()
    {
        if (null == ownedCardsUI || ownedCardsUI.IsVisible) return;
        if (false == CanOpenOwnedCards(runInfo.Phase)) return;

        ownedViewBuffer.Clear();
        RunBuildState build = runInfo.Build;
        IReadOnlyList<CardDefinitionSO> owned = build.OwnedCards;
        for (int i = 0; i < owned.Count; i++)
        {
            ownedViewBuffer.Add(ToOwnedView(owned[i], build.GetLevel(owned[i])));
        }

        ownedCardsUI.SetCards(ownedViewBuffer);

        // 여는 동안 일시정지(전투 중에 열어도 스폰·임계치 피해가 진행되지 않는다). 닫히면 OnOwnedCardsHidden이 푼다.
        PauseForOwnedCards();
        ownedCardsUI.Show();
    }

    private void OnOwnedCardsHidden() => ResumeFromOwnedCards();

    /// <summary>판이 끝났다(게임오버·포기). 결과 화면과 그 뒤 연출이 멈춘 시간 위에서 돌지 않게 보유 카드 창을 닫는다.</summary>
    private void RunEnded(RoguelikeRunEndedSignal _signal) => CloseOwnedCards();

    /// <summary>로비 진입 암전이 화면을 덮었다. 디스토션이 걸려 있던 결과 화면을 닫는다.</summary>
    private void EntryScreenCovered(RoguelikeEntryScreenCoveredSignal _signal)
    {
        if (null != resultUI && resultUI.IsVisible) resultUI.Hide();
    }

    /// <summary>
    /// 보유 카드 창을 열 수 있는 단계. 런이 진행 중인 단계만 허용한다. 쓰러짐(GameOver)·결과·로비 쪽에서 열면
    /// 일시정지가 쓰러짐 애니메이션과 결과 → 로비 연출(스케일 시간)을 창을 닫을 때까지 멈춰 세운다.
    /// 런 클리어 연출(RunClear)은 슬로우 모션(timeScale)을 직접 걸기 때문에 그동안은 열지 않는다.
    /// </summary>
    private static bool CanOpenOwnedCards(ERunPhase _phase)
    {
        switch (_phase)
        {
            case ERunPhase.RunStarting:
            case ERunPhase.CardSelect:
            case ERunPhase.TierTransition:
            case ERunPhase.WaveCombat:
            case ERunPhase.WaveClear:
                return true;
            default:
                return false;
        }
    }

    private void CloseOwnedCards()
    {
        if (null != ownedCardsUI && ownedCardsUI.IsVisible) ownedCardsUI.Hide();
        ResumeFromOwnedCards();
    }

    private void PauseForOwnedCards()
    {
        if (bOwnedCardsPaused) return;

        timeScaleBeforeOwnedCards = UnityEngine.Time.timeScale;
        UnityEngine.Time.timeScale = 0f;
        bOwnedCardsPaused = true;
    }

    private void ResumeFromOwnedCards()
    {
        if (false == bOwnedCardsPaused) return;

        bOwnedCardsPaused = false;
        UnityEngine.Time.timeScale = timeScaleBeforeOwnedCards;
    }

    // // 변환

    private void BuildCardViewData()
    {
        cardViewBuffer.Clear();

        IReadOnlyList<CardOffer> offers = runInfo.CurrentOffers;

        for (int i = 0; i < offers.Count; i++)
        {
            CardOffer offer = offers[i];
            CardDefinitionSO card = offer.card;
            if (null == card) continue;

            cardViewBuffer.Add(new RoguelikeCardViewData
            {
                cardId = card.cardId,
                nameKey = card.nameKey,
                descriptionKey = card.descriptionKey,
                card = card,
                icon = card.icon,
                kind = card.kind,
                displayGrade = offer.displayGrade,
                levelAfterPurchase = offer.levelAfterPurchase,
                maxLevel = card.EffectiveMaxLevel,
                bRuby = ECardDisplayGrade.Ruby == offer.displayGrade,
                bNewCard = offer.bNewCard,
                bFirstSeen = offer.bFirstSeen,
                bRare = CardRarity.IsRare(offer.displayGrade),
            });
        }
    }

    private static RoguelikeOwnedCardViewData ToOwnedView(CardDefinitionSO _card, int _level)
    {
        if (null == _card) return default;

        return new RoguelikeOwnedCardViewData
        {
            cardId = _card.cardId,
            nameKey = _card.nameKey,
            descriptionKey = _card.descriptionKey,
            card = _card,
            icon = _card.icon,
            kind = _card.kind,
            displayGrade = CardRarity.GetDisplayGrade(_card, _level),
            level = _level,
            maxLevel = _card.EffectiveMaxLevel,
            bRuby = ECardKind.Ruby == _card.kind,
        };
    }

    private void FillRecordCards(RunRecordEntry _entry, List<RoguelikeOwnedCardViewData> _results)
    {
        if (null == _entry.cards) return;

        CardDatabaseSO db = runInfo.CardDatabase;
        for (int i = 0; i < _entry.cards.Count; i++)
        {
            RunRecordCard recordCard = _entry.cards[i];
            CardDefinitionSO card = null != db ? db.FindById(recordCard.cardId) : null;

            if (null != card)
            {
                _results.Add(ToOwnedView(card, recordCard.level));
            }
            else
            {
                // 기록 이후 삭제된 카드. ID와 레벨만 넘긴다.
                _results.Add(new RoguelikeOwnedCardViewData { cardId = recordCard.cardId, level = recordCard.level });
            }
        }
    }

    private RoguelikeRecordViewData ToRecordView(RunRecordEntry _entry)
    {
        if (null == _entry) return default;

        List<RoguelikeOwnedCardViewData> cards = new List<RoguelikeOwnedCardViewData>(null != _entry.cards ? _entry.cards.Count : 0);
        FillRecordCards(_entry, cards);

        return new RoguelikeRecordViewData
        {
            bValid = true,
            reachedWave = _entry.reachedWave,
            bCleared = _entry.bCleared,
            unlimitWave = _entry.reachedUnlimitWave,
            playTimeSeconds = _entry.playTimeSeconds,
            rankingTimeSeconds = _entry.ClearRankingTime,
            bAbandoned = _entry.bAbandoned,
            recordedAtUtcTicks = _entry.recordedAtUtcTicks,
            cards = cards,
        };
    }
}
