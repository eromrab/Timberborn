using System.Collections.Generic;
using Timberborn.Attractions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Carrying;
using Timberborn.EnterableSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Beavers;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Reproduction;
using Timberborn.WalkingSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityEngine;

namespace Tidetails
{
  internal class RomanceRetreat : TickableComponent, IAwakableComponent, IFinishedStateListener, IBuildingEfficiencyProvider, IFinishedPausable, IUpdatableComponent
  {
    public const string RomanceNeedId = "Romance";

    private const string WaterId = "Water";

    private const string BerriesId = "Berries";

    // Points above this distance from 1.0 still need a visit. At 1.0 they do not start one.
    private const float FullTolerance = 0.001f;

    // A beaver waiting alone gives up after this long so hunger, thirst, and sleep can run.
    private const float AloneWaitHours = 2f;

    private static readonly List<RomanceRetreat> FinishedRetreats = new List<RomanceRetreat>();

    private readonly NewbornSpawner _newbornSpawner;

    private readonly IDayNightCycle _dayNightCycle;

    private readonly INavigationService _navigationService;

    private readonly WalkerService _walkerService;

    internal static bool ExpectingKit;

    internal static Child SpawnedKit;

    private Enterable _enterable;

    private bool _pairVisited;

    private bool _visitInProgress;

    private float _aloneSince = -1f;

    private bool _completing;

    private Transform _finishedModel;

    private Vector3 _modelBasePosition;

    private bool _shaking;

    public Inventory Inventory { get; private set; }

    // Stock is spent when the pair forms. CanUse stays true until that visit ends so they are not kicked out.
    public bool CanUse => IsStocked || _visitInProgress;

    public bool VisitInProgress => _visitInProgress;

    public float Efficiency => _enterable != null && _enterable.NumberOfEnterersInside >= 2 ? 1f : 0f;

    public bool IsStocked =>
      Inventory != null && Inventory.AmountInStock(WaterId) > 0 && Inventory.AmountInStock(BerriesId) > 0;

    public bool IsPaused
    {
      get
      {
        PausableBuilding pausable = GetComponent<PausableBuilding>();
        return pausable != null && pausable.Paused;
      }
    }

    public bool AloneWaitExpired =>
      _aloneSince >= 0f && _dayNightCycle.PartialDayNumber - _aloneSince >= AloneWaitHours / 24f;

    public RomanceRetreat(NewbornSpawner newbornSpawner, IDayNightCycle dayNightCycle, INavigationService navigationService, WalkerService walkerService)
    {
      _newbornSpawner = newbornSpawner;
      _dayNightCycle = dayNightCycle;
      _navigationService = navigationService;
      _walkerService = walkerService;
    }

    public void Awake()
    {
      _enterable = GetComponent<Enterable>();
      _enterable.EntererAdded += OnEntererAdded;
      _enterable.EntererRemoved += OnEntererRemoved;
    }

    public void InitializeInventory(Inventory inventory)
    {
      Inventory = inventory;
    }

    public void OnEnterFinishedState()
    {
      Inventory.Enable();
      FinishedRetreats.Add(this);
    }

    public void OnExitFinishedState()
    {
      FinishedRetreats.Remove(this);
      StopShaking();
      EjectAll();
      Inventory.Disable();
    }

    public void Update()
    {
      bool shouldShake = _visitInProgress && IsFinishedAndOpen() && !IsPaused;
      if (!shouldShake)
      {
        StopShaking();
        return;
      }
      if (!EnsureFinishedModel())
      {
        return;
      }
      float time = Time.time;
      Vector3 offset = new Vector3(Mathf.Sin(time * 28f) * 0.05f, 0f, Mathf.Sin(time * 19f) * 0.035f);
      _finishedModel.localPosition = _modelBasePosition + offset;
      _shaking = true;
    }

    public override void Tick()
    {
      if (_enterable == null)
      {
        return;
      }
      EjectChildren();
      int inside = _enterable.NumberOfEnterersInside;
      if (!IsFinishedAndOpen() || IsPaused)
      {
        if (inside > 0)
        {
          EjectAll();
        }
        return;
      }
      if (inside >= 2)
      {
        BeginVisit();
        _aloneSince = -1f;
        if (PairIsFull())
        {
          CompleteVisit();
        }
        return;
      }
      if (inside == 1)
      {
        if (_aloneSince < 0f)
        {
          _aloneSince = _dayNightCycle.PartialDayNumber;
        }
        else if (AloneWaitExpired)
        {
          EjectAll();
        }
        return;
      }
      _aloneSince = -1f;
      _pairVisited = false;
      _visitInProgress = false;
    }

    public bool TryRequestDelivery(CarrierInventoryFinder finder)
    {
      if (!IsFinishedAndOpen() || _pairVisited || IsPaused)
      {
        return false;
      }
      if (Inventory.UnreservedCapacity(WaterId) > 0 && finder.TryCarryFromAnyInventory(WaterId, Inventory))
      {
        return true;
      }
      return Inventory.UnreservedCapacity(BerriesId) > 0 && finder.TryCarryFromAnyInventory(BerriesId, Inventory);
    }

    // A visit starts only at 0 or below. Positive Romance (above 0) stays out.
    // Rock bottom (-1) is the mandatory line, matched separately with NeedIsAtMinimumPoints.
    public static bool NeedsRomance(NeedManager needs)
    {
      return needs != null && needs.HasNeed(RomanceNeedId) && !needs.NeedIsFavorable(RomanceNeedId);
    }

    public static bool IsRomanceFull(NeedManager needs)
    {
      return needs != null && needs.HasNeed(RomanceNeedId) && needs.NeedPointsToMax(RomanceNeedId) <= FullTolerance;
    }

    public static bool RomanceIsCritical(NeedManager needs)
    {
      return needs != null && needs.HasNeed(RomanceNeedId) && needs.NeedIsAtMinimumPoints(RomanceNeedId);
    }

    public bool PairIsFull()
    {
      if (_enterable == null || _enterable.NumberOfEnterersInside < 2)
      {
        return false;
      }
      foreach (Enterer enterer in _enterable.EnterersInside)
      {
        if (!IsRomanceFull(enterer.GetComponent<NeedManager>()))
        {
          return false;
        }
      }
      return true;
    }

    public void CompleteVisit()
    {
      if (_completing || !PairIsFull())
      {
        return;
      }
      _completing = true;
      BeginVisit();
      List<Enterer> parents = new List<Enterer>(_enterable.EnterersInside);
      Vector3 doorway = parents.Count > 0 ? parents[0].Transform.position : Transform.position;
      ExpectingKit = true;
      SpawnedKit = null;
      _newbornSpawner.SpawnChild(this);
      Child kit = SpawnedKit;
      ExpectingKit = false;
      SpawnedKit = null;
      _visitInProgress = false;
      EjectAll();
      WalkKitOut(kit, doorway);
      _completing = false;
    }

    internal static bool TryFindVisit(DistrictCenter district, out AttractionNeedBehavior behavior)
    {
      behavior = null;
      if (!district)
      {
        return false;
      }
      RomanceRetreat withPartner = null;
      RomanceRetreat empty = null;
      for (int i = 0; i < FinishedRetreats.Count; i++)
      {
        RomanceRetreat retreat = FinishedRetreats[i];
        if (retreat.GetComponent<DistrictBuilding>().District != district || !retreat.CanHostVisit() || !retreat._enterable.CanReserveSlot)
        {
          continue;
        }
        if (retreat._enterable.NumberOfEnterersInside > 0)
        {
          withPartner = retreat;
        }
        else if (empty == null)
        {
          empty = retreat;
        }
      }
      RomanceRetreat chosen = withPartner != null ? withPartner : empty;
      if (chosen == null)
      {
        return false;
      }
      behavior = chosen.GetComponent<AttractionNeedBehavior>();
      return behavior != null;
    }

    internal static bool TryStartDelivery(CarrierInventoryFinder finder, DistrictCenter district)
    {
      for (int i = 0; i < FinishedRetreats.Count; i++)
      {
        RomanceRetreat retreat = FinishedRetreats[i];
        if (retreat.GetComponent<DistrictBuilding>().District != district)
        {
          continue;
        }
        if (retreat.TryRequestDelivery(finder))
        {
          return true;
        }
      }
      return false;
    }

    private void OnEntererAdded(object sender, EntererAddedEventArgs args)
    {
      if (args.Enterer != null && args.Enterer.GetComponent<Child>() != null)
      {
        args.Enterer.Exit();
        return;
      }
      if (_enterable.NumberOfEnterersInside >= 2)
      {
        BeginVisit();
        _aloneSince = -1f;
      }
    }

    private void OnEntererRemoved(object sender, EntererRemovedEventArgs args)
    {
      if (_enterable.NumberOfEnterersInside == 0)
      {
        _pairVisited = false;
        _visitInProgress = false;
        _aloneSince = -1f;
      }
    }

    private bool CanHostVisit()
    {
      return IsFinishedAndOpen() && !IsPaused && IsStocked;
    }

    private void EjectAll()
    {
      if (_enterable == null)
      {
        return;
      }
      List<Enterer> inside = new List<Enterer>(_enterable.EnterersInside);
      for (int i = 0; i < inside.Count; i++)
      {
        inside[i].Exit();
      }
      _pairVisited = false;
      _visitInProgress = false;
      _aloneSince = -1f;
    }

    // Berries and water are the price of the visit, paid when the second adult arrives and Romance starts rising.
    private void BeginVisit()
    {
      if (_visitInProgress || _enterable == null || _enterable.NumberOfEnterersInside < 2 || !IsStocked)
      {
        return;
      }
      ConsumeAll(WaterId);
      ConsumeAll(BerriesId);
      _visitInProgress = true;
      _pairVisited = true;
    }

    private void EjectChildren()
    {
      List<Enterer> children = new List<Enterer>();
      foreach (Enterer enterer in _enterable.EnterersInside)
      {
        if (enterer.GetComponent<Child>() != null)
        {
          children.Add(enterer);
        }
      }
      for (int i = 0; i < children.Count; i++)
      {
        children[i].Exit();
      }
    }

    private void WalkKitOut(Child kit, Vector3 doorway)
    {
      if (kit == null)
      {
        return;
      }
      kit.Transform.position = doorway;
      Vector3 away = doorway - Transform.position;
      away.y = 0f;
      if (away.sqrMagnitude < 0.01f)
      {
        away = Vector3.forward;
      }
      Walker walker = kit.GetComponent<Walker>();
      if (walker == null)
      {
        return;
      }
      Vector3 target = doorway + away.normalized * 1.5f;
      walker.GoTo(new PositionDestination(_navigationService, _walkerService, target, 0.2f));
    }

    private bool EnsureFinishedModel()
    {
      if (_finishedModel != null)
      {
        return true;
      }
      BuildingModel buildingModel = GetComponent<BuildingModel>();
      if (buildingModel == null || buildingModel.FinishedModel == null)
      {
        return false;
      }
      _finishedModel = buildingModel.FinishedModel.transform;
      _modelBasePosition = _finishedModel.localPosition;
      return true;
    }

    private void StopShaking()
    {
      if (!_shaking || _finishedModel == null)
      {
        _shaking = false;
        return;
      }
      _finishedModel.localPosition = _modelBasePosition;
      _shaking = false;
    }

    private void ConsumeAll(string goodId)
    {
      int amount = Inventory.AmountInStock(goodId);
      if (amount > 0)
      {
        Inventory.TakeConsumed(new GoodAmount(goodId, amount));
      }
    }

    private bool IsFinishedAndOpen()
    {
      BlockObject blockObject = GetComponent<BlockObject>();
      return blockObject != null && blockObject.IsFinished;
    }
  }
}
