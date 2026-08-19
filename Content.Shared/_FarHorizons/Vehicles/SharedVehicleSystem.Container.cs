using Content.Shared._FarHorizons.Vehicles.Components;
using Content.Shared._FarHorizons.Vehicles.Events;
using System.Linq;
using Robust.Shared.Containers;

namespace Content.Shared._FarHorizons.Vehicles;

public abstract partial class SharedVehicleSystem : EntitySystem
{    
    public void InitializeContainer()
    {
        SubscribeLocalEvent<VehicleContainerComponent, VehicleEntryDoAfter>(OnVehicleEntryDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, VehicleRemoveDoAfter>(OnVehicleRemoveDoAfter);
        SubscribeLocalEvent<VehicleContainerComponent, EntInsertedIntoContainerMessage>(OnEntInserted, after: [typeof(SharedContainerSystem)]);
    }

    private void OnVehicleEntryDoAfter(Entity<VehicleContainerComponent> ent, ref VehicleEntryDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        if(!TryInsert(args.Args.Target, ent.Owner, ent.Comp)) return;

        SetUpRider(args.Args.Target!.Value, ent.Owner, vehicleComp);

        args.Handled = true;
    }

    private void OnVehicleRemoveDoAfter(Entity<VehicleContainerComponent> ent, ref VehicleRemoveDoAfter args)
    {
        if (args.Cancelled || args.Handled)
            return;
        
        if(!TryComp<VehicleComponent>(ent, out var vehicleComp)) return;
        var passenger = ent.Comp.PassengerSlot.ContainedEntities.FirstOrDefault();
        if(passenger == default) return;
        RemoveRider(passenger, ent.Owner, vehicleComp);
        TryRemove(passenger, ent.Owner, ent.Comp);

        args.Handled = true;
    }

    private void OnEntInserted(EntityUid ent, VehicleContainerComponent component, EntInsertedIntoContainerMessage args)
    {
        if(args.Container != component.PassengerSlot) return;
        
        var tagert = args.Entity; 
        if(_whitelist.IsWhitelistFail(component.PassengerWhitelist, tagert))
        {
            if(HasComp<RiderComponent>(tagert) && TryComp<VehicleComponent>(ent, out var vehicleComp))
                RemoveRider(tagert, ent, vehicleComp);

            if(_tags.HasTag(tagert, s_vehicleKeyTag)) return;
                
            _container.Remove(tagert, component.PassengerSlot);
        }
    }
}