using Content.Shared._FarHorizons.Vehicles.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.DragDrop;
using Content.Shared.Lock;
using Robust.Shared.Timing;
using Content.Shared.Examine;
using Content.Shared.Buckle;
using Content.Shared.Movement.Components;
using Content.Shared._FarHorizons.Vehicles.Events;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using System.Linq;
using Content.Shared.PowerCell;
using Content.Shared._FarHorizons.ReagentDraw.Components;
using Content.Shared.Audio;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.Destructible;
using Content.Shared.Whitelist;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Effects;
using Robust.Shared.Player;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.PowerCell.Components;
using Content.Shared.Hands;
using Content.Shared._FarHorizons.ReagentDraw.EntitySystems;

namespace Content.Shared._FarHorizons.Vehicles;

public abstract partial class SharedVehicleSystem : EntitySystem
{    
    [Dependency] protected ISharedAdminLogManager _adminLogger = default!;
    [Dependency] protected SharedMoverController _mover = default!;
    [Dependency] protected SharedTransformSystem _transform = default!;
    [Dependency] protected SharedBuckleSystem _buckle = default!;
    [Dependency] protected ActionBlockerSystem _actionBlocker = default!;
    [Dependency] protected SharedActionsSystem _actions = default!;
    [Dependency] protected TagSystem _tags = default!;
    [Dependency] protected PowerCellSystem _powerCell = default!;
    [Dependency] protected SharedReagentDrawSystem _reagentDraw = default!;
    [Dependency] protected SharedStunSystem _stun = default!;
    [Dependency] protected SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] protected SharedPopupSystem _popup = default!;
    [Dependency] protected SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected SharedHandsSystem _handsSystem = default!;
    [Dependency] protected IGameTiming _gameTiming = default!;
    [Dependency] protected SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] protected SharedContainerSystem _container = default!;
    [Dependency] protected DamageableSystem _damageable = default!;
    [Dependency] protected EntityWhitelistSystem _whitelist = default!;
    [Dependency] protected LockSystem _lock = default!;
    [Dependency] protected SharedColorFlashEffectSystem _color = default!;
    [Dependency] protected SharedGunSystem _gun = default!;
    [Dependency] protected SharedAudioSystem _audio = default!;
    [Dependency] protected SharedAppearanceSystem _appearance = default!;
    protected static readonly ProtoId<TagPrototype> s_vehicleKeyTag = "VehicleKey";
    public override void Initialize()
    {
        base.Initialize();

        InitializeRider();
        InitializeBuckle();
        InitializeContainer();
        
        SubscribeLocalEvent<VehicleComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VehicleComponent, EntInsertedIntoContainerMessage>(OnEntInsertedVehicle, after: [typeof(SharedContainerSystem)]);
        SubscribeLocalEvent<VehicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<VehicleComponent, ItemSlotInsertEvent>(OnInsertEvent);
        SubscribeLocalEvent<VehicleComponent, ItemSlotEjectEvent>(OnEjectEvent, before: [typeof(SharedHandsSystem)]);
        SubscribeLocalEvent<VehicleComponent, EjectKeysDoAfter>(OnEjectKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, TurnKeysDoAfter>(OnTurnKeysDoAfter);
        SubscribeLocalEvent<VehicleComponent, ReagantContainerSlotEmptyEvent>(OnEmptyReagantContainer);
        SubscribeLocalEvent<VehicleComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<VehicleComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<VehicleComponent, BreakageEventArgs>(OnBreakageEvent);
        SubscribeLocalEvent<VehicleComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<VehicleComponent, TurnKeysEvent>(OnTurnKeysEvent);
        SubscribeLocalEvent<VehicleComponent, HornActionEvent>(OnHornActionEvent);
        SubscribeLocalEvent<VehicleComponent, ToggleTrunkActionEvent>(OnToggleTrunk);
        SubscribeLocalEvent<VehicleComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<VehicleComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<TransformComponent, JetJumpActionEvent>(OnJetJumpActionEvent);
        SubscribeLocalEvent<DidEquipHandEvent>(OnHandEquipped);
        _transform.OnGlobalMoveEvent += OnMoveEvent;
    }

    private void OnComponentStartup(Entity<VehicleComponent> ent, ref ComponentStartup args)
    {
        if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp))
        {
            vcComp.PassengerSlot = _container.EnsureContainer<Container>(ent.Owner, vcComp.PassengerSlotId);
            Dirty(ent.Owner, vcComp);
        }
        EnsureComp<VehicleActionsComponent>(ent.Owner);
        Dirty(ent);
    }

    private void OnEntInsertedVehicle(Entity<VehicleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if(args.Container.ID != "key_slot") return;
        ent.Comp.hasKeys = _tags.HasTag(args.Entity, s_vehicleKeyTag);
        Dirty(ent);
    }

    private void OnInsertEvent(Entity<VehicleComponent> ent, ref ItemSlotInsertEvent args)
    {
        if(_tags.HasTag(args.Item, s_vehicleKeyTag))
        {
            ent.Comp.hasKeys = true;
            Dirty(ent.Owner, ent.Comp);
            var target = args.User;
            if(target != null)
            {
                if(TryComp<BuckleComponent>(target, out var buckleComp) && buckleComp.BuckledTo == ent.Owner && ent.Comp.Rider == null)
                    SetUpRider(target.Value, ent.Owner, ent.Comp);
                if(TryComp<VehicleContainerComponent>(ent.Owner, out var vcComp) && vcComp.PassengerSlot.ContainedEntities.Any(x => x == target))
                    SetUpRider(target.Value, ent.Owner, ent.Comp);
            }
        }
    }

    private void OnEjectEvent(Entity<VehicleComponent> ent, ref ItemSlotEjectEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if(args.User == null) return;
        var user = args.User;
        var item = args.Item;
        if(_tags.HasTag(args.Item, s_vehicleKeyTag))
        {
            if(ent.Comp.Rider == user || ent.Comp.Rider == null)
            {
                ent.Comp.hasKeys = false;
                if(ent.Comp.Rider != null)
                {
                    UpdateActions(ent.Comp.Rider.Value, false);
                    if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                        _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

                    for (var i = 0; i < ent.Comp.HandsNeeded; i++)
                    {
                        _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
                    }
                }

                TurnOffVehicle(ent.Owner, ent.Comp);
                _handsSystem.PickupOrDrop(user, item);
                Dirty(ent);
            }
            else
            {
                args.Cancelled = true;
                _popup.PopupClient(Loc.GetString("vehicle-steal-keys-attempt"), ent.Owner, PopupType.LargeCaution);
                var ev = new EjectKeysDoAfter();
                var doAfter = new DoAfterArgs(EntityManager, args.User.Value, ent.Comp.timeToStealKeys, ev, ent.Owner, ent.Owner)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    CancelDuplicate = false

                };
                _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Medium, $"{ToPrettyString(args.User.Value)} began to attempt to steal keys from {ToPrettyString(ent.Owner)}");
                _doAfter.TryStartDoAfter(doAfter);
            }
        }
    }

    private void OnEjectKeysDoAfter(Entity<VehicleComponent> ent, ref EjectKeysDoAfter args)
    {
        if(args.Cancelled || args.Handled) return;
        if(TryComp<ContainerManagerComponent>(ent.Owner, out var container))
        {
            var key = container.Containers.Values.SelectMany(c => c.ContainedEntities).FirstOrDefault(e => _tags.HasTag(e, s_vehicleKeyTag));           
            ent.Comp.hasKeys = false;
            TurnOffVehicle(ent.Owner, ent.Comp);
            if(ent.Comp.Rider == null) return;
            
            UpdateActions(ent.Comp.Rider.Value, false);
            if(TryComp<InputMoverComponent>(ent.Comp.Rider.Value, out var imComp) && imComp.CanMove)
                _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);
                
            _handsSystem.PickupOrDrop(args.User, key);
            ent.Comp.Rider = null;
            Dirty(ent);
        }
        args.Handled = true;
    }

    private void OnTurnKeysEvent(Entity<VehicleComponent> ent, ref TurnKeysEvent args)
    {
        if(args.Handled || ent.Comp.Rider == null) return;
        if(!TryComp<MovementSpeedModifierComponent>(ent.Owner, out var msmComp) || msmComp.BaseSprintSpeed <= 0)
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-fail"), ent.Comp.Rider.Value, PopupType.SmallCaution);
            return;
        }
        if(!ent.Comp.Started)
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-start"), ent.Comp.Rider.Value, PopupType.Medium);
            _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} started the engine of {ToPrettyString(ent.Owner)}");
            _audio.PlayPredicted(ent.Comp.StartUp, ent.Owner, ent.Comp.Rider.Value);
        }
        if(ent.Comp.Started)
        {
            _popup.PopupClient(Loc.GetString("vehicle-turn-keys-stop"), ent.Comp.Rider.Value, PopupType.Medium);
            _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(ent.Comp.Rider.Value)} stopped the engine of {ToPrettyString(ent.Owner)}");
        }        
        var ev = new TurnKeysDoAfter();
        var doAfter = new DoAfterArgs(EntityManager, ent.Comp.Rider.Value, ent.Comp.startupTime, ev, ent.Owner)
        {
            BreakOnMove = true
        };
        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }
    
    private void OnTurnKeysDoAfter(Entity<VehicleComponent> ent, ref TurnKeysDoAfter args)
    {
        if(args.Cancelled) return;
        if(ent.Comp.Rider == null) return;
        
        if(!ent.Comp.Started)
        {
            if((ent.Comp.CellPowered && HasComp<PowerCellDrawComponent>(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner)) 
            ^ (!ent.Comp.CellPowered && HasComp<ReagentDrawComponent>(ent.Owner) && !_reagentDraw.HasDrawReagant(ent.Owner)))
                return;

            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, ent.Comp.Rider.Value, out var virtItem, true, silent: true))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }
        }

        if(ent.Comp.Started)
        {
            for (var i = 0; i < ent.Comp.HandsNeeded; i++)
            {
                _virtualItem.DeleteInHandsMatching(ent.Comp.Rider.Value, ent.Owner);
            }
        }

        ent.Comp.Started = !ent.Comp.Started;
        if(ent.Comp.CellPowered && TryComp<PowerCellDrawComponent>(ent.Owner, out var pcdComp))
        {
            _powerCell.SetDrawEnabled((ent.Owner, pcdComp), ent.Comp.Started);
        }
        if(!ent.Comp.CellPowered && TryComp<ReagentDrawComponent>(ent.Owner, out var rdComp))
        {
            rdComp.Enabled = ent.Comp.Started;
            Dirty(ent.Owner, rdComp);
            _ambientSound.SetAmbience(ent.Owner, ent.Comp.Started);
        }

        _actionBlocker.UpdateCanMove(ent.Comp.Rider.Value);

        Dirty(ent.Owner, ent.Comp);
    }

    private void OnGetAdditionalAccess(Entity<VehicleComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (ent.Comp.Rider == null) return;

        args.Entities.Add(ent.Comp.Rider.Value);
    }

    private void OnEmptyReagantContainer(Entity<VehicleComponent> ent, ref ReagantContainerSlotEmptyEvent args)
    {
        if(!ent.Comp.CellPowered)
            TurnOffVehicle(ent.Owner, ent.Comp);
    }

    private void OnPowerCellEmpty(Entity<VehicleComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if(ent.Comp.CellPowered)
            TurnOffVehicle(ent.Owner, ent.Comp);
    }

    private void OnToggleTrunk(Entity<VehicleComponent> ent, ref ToggleTrunkActionEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted) return;
        if(args.Handled) return;
        if(!TryComp<LockComponent>(ent.Owner, out var lockComp)) return;
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(args.Performer)} toggled the trunk from {ToPrettyString(ent.Owner)}");
        _lock.ToggleLock(ent.Owner, args.Performer, lockComp);

        if(!_lock.IsLocked(ent.Owner))
        {
            _popup.PopupPredicted(Loc.GetString("vehicle-toggle-trunk-open"), ent.Owner, null, PopupType.Small);
            _audio.PlayPredicted(lockComp.UnlockSound, ent.Owner, null);
        }
        else
        {
            _popup.PopupPredicted(Loc.GetString("vehicle-toggle-trunk-close"), ent.Owner, null, PopupType.Small);
            _audio.PlayPredicted(lockComp.LockSound, ent.Owner, null);
        }
        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid ent, VehicleComponent component, DamageChangedEvent args)
    {
        if(!args.DamageIncreased || args.DamageDelta == null) return;
        if(args.Origin == ent) return;
        if (TryComp<VehicleContainerComponent>(ent, out var vcComp)
            && vcComp.PassengerSlot.ContainedEntities.Count != 0)
        {
            var damage = args.DamageDelta * vcComp.DamageTransferMultiplier;
            foreach(var passenger in vcComp.PassengerSlot.ContainedEntities)
            {
                _damageable.TryChangeDamage(passenger, damage / vcComp.PassengerSlot.ContainedEntities.Count, origin: args.Origin);
            }
        }
        else if(HasComp<VehicleBuckleComponent>(ent) && component.Rider != null && !TerminatingOrDeleted(component.Rider.Value))
        {
            _damageable.TryChangeDamage(component.Rider.Value, args.DamageDelta, origin: args.Origin);
            _color.RaiseEffect(Color.Red, new List<EntityUid>() { component.Rider.Value }, Filter.Pvs(component.Rider.Value, entityManager: EntityManager));
        }
    }

    private void OnEmpPulse(Entity<VehicleComponent> ent, ref EmpPulseEvent args) => TurnOffVehicle(ent.Owner, ent.Comp);

    private void OnBreakageEvent(EntityUid ent, VehicleComponent component, BreakageEventArgs args)
    {
        if(TryComp<VehicleContainerComponent>(ent, out var vcComp))
        {
            if(vcComp.PassengerSlot.ContainedEntities.Count != 0)
            {
                foreach(var passengers in vcComp.PassengerSlot.ContainedEntities.ToArray())
                {
                    RemoveRider(passengers, ent, component);
                    TryRemove(passengers, ent, vcComp);
                }
            }
        }
        if(TryComp<VehicleBuckleComponent>(ent, out var vbComp))
        {
            _buckle.StrapSetEnabled(ent, false);
        }
        
        component.isBroken = true;

        TryUpdateVisualState(ent);

        TurnOffVehicle(ent, component);
    }

    private void OnExamine(Entity<VehicleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if(ent.Comp.isBroken)
            args.PushMarkup(Loc.GetString("vehicle-examine-broken"));
    }

    private void OnCanDragDrop(Entity<VehicleComponent> ent, ref CanDropTargetEvent args)
    {
        args.CanDrop = !ent.Comp.isBroken;
        args.Handled = true;
    } 

    public void TryUpdateVisualState(Entity<VehicleComponent?> entity)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (_gameTiming.ApplyingState)
            return;

        var finalState = VehicleVisualState.Normal;

        if (entity.Comp.isBroken)
        {
            finalState = VehicleVisualState.Broken;
        }
        else if (entity.Comp.isMoving)
        {
            finalState = VehicleVisualState.Moving;
        }
        _appearance.SetData(entity.Owner, VehicleVisuals.VisualState, finalState);
    }
    #region Misc Events

    private void OnJetJumpActionEvent(Entity<TransformComponent> ent, ref JetJumpActionEvent args)
    {
        if(!TryComp<BuckleComponent>(ent.Comp.ParentUid, out var buckleComp)) return;
        _buckle.Unbuckle((ent.Comp.ParentUid, buckleComp), ent.Comp.ParentUid);
    }

    //The pickable races check...
    private void OnHandEquipped(DidEquipHandEvent ev)
    {
        if(!TryComp<RiderComponent>(ev.Equipped, out var riderComp) 
            || riderComp.Riding == null
            || !TryComp<VehicleComponent>(riderComp.Riding.Value, out var vehicleComp)) return;
        RemoveRider(ev.Equipped, riderComp.Riding.Value, vehicleComp);
    }

    private void OnMoveEvent(ref MoveEvent ev)
    {
        var vehicle = ev.Entity.Owner; 
        if(!TryComp<VehicleComponent>(vehicle, out var vehicleComp)) return;
        if(!TryComp<PhysicsComponent>(vehicle, out var vehiclePhys)) return;

        var speed = vehiclePhys.LinearVelocity.Length();
        if(speed >= 0.3)
            vehicleComp.isMoving = true;
        else if(speed < 0.3)
            vehicleComp.isMoving = false;
            
        TryUpdateVisualState(vehicle);  
        Dirty(vehicle, vehicleComp);

        if( vehicleComp.Rider == null) return;
        var rider = vehicleComp.Rider.Value;

        if (!rider.IsValid() || !Exists(rider)) return;

        var riderTransform = Transform(rider);
        if(riderTransform.ParentUid !=  vehicle) return;

        if(HasComp<VehicleBuckleComponent>(vehicle) && TryComp<StrapComponent>(vehicle, out var strapComp))
        {
            if(!riderTransform.ActivelyLerping && !_gameTiming.ApplyingState)
            {
                if(riderTransform.LocalPosition.X != 0+strapComp.BuckleOffset.X || riderTransform.LocalPosition.Y != 0+strapComp.BuckleOffset.Y)
                _transform.SetLocalPosition(rider, new Vector2(0f+strapComp.BuckleOffset.X, 0f+strapComp.BuckleOffset.Y), riderTransform);
                if(riderTransform.LocalRotation != 0)
                    _transform.SetLocalRotation(rider, 0f, riderTransform);
            }
        }
    }
    
    #endregion
    #region Functions
    public void SetUpRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        var riderComp = EnsureComp<RiderComponent>(rider);
        riderComp.Riding = vehicle;
        Dirty(rider, riderComp);
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(rider)} entered vehicle {ToPrettyString(vehicle)}");
        if(TryComp<InputMoverComponent>(rider, out var imComp) && imComp.CanMove)
            _actionBlocker.UpdateCanMove(rider);

        foreach(var item in _handsSystem.EnumerateHeld(rider))
        {
            if(HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }

        if(_whitelist.IsWhitelistFail(vehicleComp.RiderWhitelist, rider)) return;
        if(!vehicleComp.hasKeys && vehicleComp.RequireIgnition) return;
        if(vehicleComp.Rider != null) return;
        
        _actions.GrantContainedActions(rider, vehicle);
        UpdateActions(rider, true);

        if (!TryComp<RelayInputMoverComponent>(rider, out var relay) || relay.RelayEntity != vehicle)
            _mover.SetRelay(rider, vehicle);
        vehicleComp.Rider = rider;
        Dirty(vehicle, vehicleComp);
        
        if(vehicleComp.Started)
        {
            for (var i = 0; i < vehicleComp.HandsNeeded; i++)
            {
                if (_virtualItem.TrySpawnVirtualItemInHand(vehicle, rider, out var virtItem, true, silent: true))
                    EnsureComp<UnremoveableComponent>(virtItem.Value);
            }
        }
    }

    private void UpdateActions(EntityUid rider, bool gettingOn)
    {
        if(!TryComp<RiderComponent>(rider, out var riderComp) || riderComp.Riding == null) return;
        var vehicle = riderComp.Riding.Value;

        if(!TryComp<VehicleActionsComponent>(vehicle, out var vaComp) 
            || !TryComp<VehicleComponent>(vehicle, out var vehicleComp)) return;

        if(gettingOn)
        {
            if(vaComp.TurnKeysActionEntity == null && vehicleComp.hasKeys)
                _actions.AddAction(rider, ref vaComp.TurnKeysActionEntity, vaComp.TurnKeysAction, vehicle);
            if(HasComp<LockComponent>(vehicle) && vaComp.ToggleTrunkActionEntity == null && vehicleComp.hasKeys)
                _actions.AddAction(rider, ref vaComp.ToggleTrunkActionEntity, vaComp.ToggleTrunkAction, vehicle);
            if(vehicleComp.HornSound != null && vaComp.HornVehicleActionEntity == null)
                _actions.AddAction(rider, ref vaComp.HornVehicleActionEntity, vaComp.HornVehicleAction, vehicle);

            var addingActions = new AddRiderActions(rider);
            RaiseLocalEvent(rider, ref addingActions);
        }
        else if(!gettingOn)
        {
            if(vaComp.TurnKeysActionEntity != null && !vehicleComp.hasKeys)
            {
                _actions.RemoveAction(rider, vaComp.TurnKeysActionEntity);
                QueueDel(vaComp.TurnKeysActionEntity);
                vaComp.TurnKeysActionEntity = null;
            }
            if(HasComp<LockComponent>(vehicle) && vaComp.ToggleTrunkActionEntity != null && !vehicleComp.hasKeys)
            {
                _actions.RemoveAction(rider, vaComp.ToggleTrunkActionEntity);
                QueueDel(vaComp.ToggleTrunkActionEntity);
                vaComp.ToggleTrunkActionEntity = null;
            }
            if(vehicleComp.HornSound != null && vaComp.HornVehicleActionEntity != null)
            {
                _actions.RemoveAction(rider, vaComp.HornVehicleActionEntity);
                QueueDel(vaComp.HornVehicleActionEntity);
                vaComp.HornVehicleActionEntity = null;
            }

            var removingActions = new RemoveRiderActions(rider);
            RaiseLocalEvent(rider, ref removingActions);
        }

        Dirty(vehicle, vehicleComp);
    } 

    public void RemoveRider(EntityUid rider, EntityUid vehicle, VehicleComponent vehicleComp)
    {
        _adminLogger.Add(Database.LogType.Action, Database.LogImpact.Low, $"{ToPrettyString(rider)} exited vehicle {ToPrettyString(vehicle)}");
        foreach(var item in _handsSystem.EnumerateHeld(rider))
        {
            if(HasComp<GunComponent>(item))
                _gun.RefreshModifiers(item);
        }

        if(rider == vehicleComp.Rider)
        {
            UpdateActions(rider, false);
            _actions.RemoveProvidedActions(rider, vehicle);
            vehicleComp.Rider = null;
            
            for (var i = 0; i < vehicleComp.HandsNeeded; i++)
            {
                _virtualItem.DeleteInHandsMatching(rider, vehicle);
            }
        }

        if(HasComp<RelayInputMoverComponent>(rider))
            RemComp<RelayInputMoverComponent>(rider);
        if(HasComp<RiderComponent>(rider))
            RemComp<RiderComponent>(rider);
                
        if(TryComp<InputMoverComponent>(rider, out var imComp) && !imComp.CanMove)
            _actionBlocker.UpdateCanMove(rider);

        Dirty(vehicle, vehicleComp);
    }

    private bool TryInsert(EntityUid? Rider, EntityUid Vehicle, VehicleContainerComponent? component=null)
    {
        if(!Resolve(Vehicle, ref component))
            return false;

        if(Rider == null)
            return false;
                
        if (!CanInsert(Vehicle, component))
            return false;

        _container.Insert(Rider.Value, component.PassengerSlot);
        Dirty(Vehicle, component);
        return true;
    }

    public bool CanInsert(EntityUid uid, VehicleContainerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.PassengerSlot.ContainedEntities.Count() < component.Seats;
    }

    public bool TryRemove(EntityUid? Rider, EntityUid Vehicle, VehicleContainerComponent? component=null)
    {
        if(!Resolve(Vehicle, ref component))
            return false;

        if(Rider == null)
            return false;

        _container.Remove(Rider.Value, component.PassengerSlot);
        Dirty(Vehicle, component);
        return true;
    }

    private void TurnOffVehicle(EntityUid vehicle, VehicleComponent? component=null)
    {
        if(!Resolve(vehicle, ref component))
            return;
            
        var ev = new TurnOffVehicleEvent();
        RaiseLocalEvent(vehicle, ref ev);

        if(component.Started)
            component.Started = false;

        if(component.CellPowered && TryComp<PowerCellDrawComponent>(vehicle, out var pcdComp) && pcdComp.Enabled)
        {
            _powerCell.SetDrawEnabled((vehicle, pcdComp), false);
        }   
        if(!component.CellPowered && TryComp<ReagentDrawComponent>(vehicle, out var rdComp) && rdComp.Enabled)
        {
            rdComp.Enabled = false;
            _ambientSound.SetAmbience(vehicle, rdComp.Enabled);
            Dirty(vehicle, rdComp);
        }

        if(component.Rider != null)  
            if(TryComp<InputMoverComponent>(component.Rider.Value, out var imComp) && imComp.CanMove)
            {
                _actionBlocker.UpdateCanMove(component.Rider.Value);
                for (var i = 0; i < component.HandsNeeded; i++)
                {
                    _virtualItem.DeleteInHandsMatching(component.Rider.Value, vehicle);
                }
            }

        Dirty(vehicle, component);
    }
    #endregion
}