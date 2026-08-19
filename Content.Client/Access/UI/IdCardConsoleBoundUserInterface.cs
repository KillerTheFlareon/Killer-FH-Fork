using Content.Shared.Access;
using Content.Shared.Access.Components;
// FH start
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewManifest;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using Robust.Client.UserInterface;
using Content.Shared._FarHorizons.Factions;
// FH end

namespace Content.Client.Access.UI
{
    public sealed partial class IdCardConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private IConfigurationManager _cfgManager = default!;

        private IdCardConsoleWindow? _window;

        // CCVar.
        private int _maxNameLength;
        private int _maxIdJobLength;

        public IdCardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _maxNameLength =_cfgManager.GetCVar(CCVars.MaxNameLength);
            _maxIdJobLength = _cfgManager.GetCVar(CCVars.MaxIdJobLength);
        }

        protected override void Open()
        {
            base.Open();
            // FH start
            _window = this.CreateWindow<IdCardConsoleWindow>();
            
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            var test = EntMan.GetComponent<IdCardConsoleComponent>(Owner).Factions;
            _window.ComputerFactions = EntMan.GetComponent<IdCardConsoleComponent>(Owner).Factions; // FH
            _window.Initialize(this);
            // FH end
            _window.CrewManifestButton.OnPressed += _ => SendMessage(new CrewManifestOpenUiMessage());
            _window.PrivilegedIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(PrivilegedIdCardSlotId));
            _window.TargetIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(TargetIdCardSlotId));

            _window.OnClose += Close;
            _window.OpenCentered();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _window?.Dispose();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            var castState = (IdCardConsoleBoundUserInterfaceState) state;
            _window?.UpdateState(castState);
        }

        public void SubmitData(string newFullName, string newJobTitle, List<ProtoId<AccessLevelPrototype>> newAccessList, ProtoId<FactionJobAssignmentPrototype> newJobPrototype) //FH
        {
            if (newFullName.Length > _maxNameLength)
                newFullName = newFullName[.._maxNameLength];

            if (newJobTitle.Length > _maxIdJobLength)
                newJobTitle = newJobTitle[.._maxIdJobLength];

            SendMessage(new WriteToTargetIdMessage(
                newFullName,
                newJobTitle,
                newAccessList,
                newJobPrototype));
        }
        // Starlight-edit: Start

        public void OnGroupSelected(ProtoId<AccessGroupPrototype> group)
        {
            SendMessage(new IdCardConsoleComponent.AccessGroupSelectedMessage(group)); // Starlight-edit
        }
        // Starlight-edit: End
    }
}
