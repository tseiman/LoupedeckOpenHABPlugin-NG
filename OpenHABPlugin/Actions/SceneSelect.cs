using System;
using Loupedeck.OpenHABPluginNG.MultiWheel;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public sealed class SceneSelect : ActionEditorCommand
    {
        private OpenHABPlugin Owner => (OpenHABPlugin)this.Plugin;
        public SceneSelect()
        {
            this.Name = GetType().Name;
            this.DisplayName = "Scene Select ON/OFF";
            this.GroupName = "Scenes";
            this.IsWidget = true;
            this.ActionEditor.AddControlEx(new ActionEditorListbox("tag", "Scene group (tag):").SetRequired());
            this.ActionEditor.ListboxItemsRequested += (_, e) =>
            {
                if (!string.Equals(e.ControlName, "tag", StringComparison.OrdinalIgnoreCase)) return;
                foreach (var tag in Owner.OHService.SceneTags) e.AddItem(tag, tag, null);
            };
        }
        protected override bool OnLoad()
        {
            var loaded = base.OnLoad();
            Owner.SceneSelection.Changed += Changed;
            Owner.OHService.ScenesChanged += CatalogChanged;
            _ = Owner.OHService.RefreshScenesAsync();
            return loaded;
        }
        protected override bool OnUnload()
        {
            Owner.SceneSelection.Changed -= Changed;
            Owner.OHService.ScenesChanged -= CatalogChanged;
            Owner.SceneSelection.Cancel();
            return base.OnUnload();
        }
        private void Changed(object sender, EventArgs e) => this.ActionImageChanged();
        private void CatalogChanged(object sender, EventArgs e) => this.ActionEditor.ListboxItemsChanged("tag");
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            var tag = parameters.GetString("tag", "");
            if (string.IsNullOrWhiteSpace(tag)) return false;
            _ = Owner.SceneSelection.ToggleAsync(tag);
            return true;
        }
        protected override string GetCommandDisplayName(ActionEditorActionParameters parameters) => "Scene Select";
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, int width, int height)
        {
            var selection = Owner.SceneSelection;
            var active = selection.Active && selection.Tag == parameters.GetString("tag", "");
            return ScenePresentation.Render(width, height, "Scene", active ? "ON" :
                selection.Message.Length > 0 && !selection.Active ? selection.Message : "OFF", active, Owner.OHService.GetSceneIconSvg());
        }
    }
}
