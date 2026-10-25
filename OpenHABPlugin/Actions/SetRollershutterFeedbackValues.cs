using System;

namespace Loupedeck.OpenHABPluginNG.Actions
{
    public sealed class SetRollershutterFeedbackValues : ActionEditorCommand
    {
        public SetRollershutterFeedbackValues()
        {
            this.Name = GetType().Name;
            this.DisplayName = "Apply rollershutter feedback values";
            this.GroupName = "Settings";
            this.ActionEditor.AddControlEx(new ActionEditorTextbox("stopped", "Stopped value (default 0):").SetPlaceholder("0"));
            this.ActionEditor.AddControlEx(new ActionEditorTextbox("up", "Moving up value (default 1):").SetPlaceholder("1"));
            this.ActionEditor.AddControlEx(new ActionEditorTextbox("down", "Moving down value (default 2):").SetPlaceholder("2"));
        }
        internal static RollershutterFeedbackValues ReadValues(ActionEditorActionParameters parameters) => new RollershutterFeedbackValues(
            parameters.GetString("stopped", "0"), parameters.GetString("up", "1"), parameters.GetString("down", "2"));
        protected override bool RunCommand(ActionEditorActionParameters parameters)
        {
            try
            {
                ((OpenHABPlugin)this.Plugin).SetRollershutterFeedbackValues(ReadValues(parameters));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[OpenHAB NG] Feedback mapping not applied: " + ex.GetType().Name);
                return false;
            }
        }
        protected override string GetCommandDisplayName(ActionEditorActionParameters parameters) => "Apply movement values";
    }
}
