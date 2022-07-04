using System.Text;
using Content.Shared.Forensics;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Forensics
{
    [GenerateTypedNameReferences]
    public sealed partial class ForensicScannerMenu : DefaultWindow
    {
        public ForensicScannerMenu()
        {
            RobustXamlLoader.Load(this);
        }

        public void Populate(ForensicScannerUserMessage msg)
        {
            Print.Disabled = false;
            var text = new StringBuilder();

            text.AppendLine(Loc.GetString("forensic-scanner-interface-fingerprints"));
            foreach (var fingerprint in msg.Fingerprints)
            {
                text.AppendLine(fingerprint);
            }
            text.AppendLine();
            text.AppendLine(Loc.GetString("forensic-scanner-interface-fibers"));
            foreach (var fiber in msg.Fibers)
            {
                text.AppendLine(fiber);
            }
            Diagnostics.Text = text.ToString();
            SetSize = (350, 600);
        }
    }
}
