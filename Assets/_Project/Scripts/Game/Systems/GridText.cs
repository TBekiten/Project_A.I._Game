using System.Text;
using AIGame.Core.Energy;

namespace AIGame.Game.Systems
{
    /// <summary>Şebeke elemanları için ipucu (tooltip) metinleri.</summary>
    public static class GridText
    {
        public static string Describe(PowerGrid grid, GridEvaluation evaluation, string elementId, bool interactive)
        {
            if (grid == null || evaluation == null || string.IsNullOrEmpty(elementId)) return null;
            var sb = new StringBuilder();

            if (grid.HasLine(elementId))
            {
                var line = grid.Line(elementId);
                sb.Append("<b>").Append(line.Name).Append("</b>\n");
                sb.Append("Akış: ").Append(evaluation.FlowOf(line.Id)).Append(" / ").Append(line.Capacity).Append('\n');
                sb.Append("Durum: ").Append(LineState(line, evaluation));
                if (line.AutomaticTransfer) sb.Append("\nOtomatik transfer hattı");
                if (!line.Switchable) sb.Append("\n<color=#8a96a6>Uzaktan kumanda edilemez</color>");
                else if (interactive && !line.Faulted)
                    sb.Append("\n<color=#7fe0ff>Tıkla: ").Append(line.Closed ? "hattı aç (enerjiyi kes)" : "hattı kapat (enerji ver)").Append("</color>");
                return sb.ToString();
            }

            if (!grid.HasNode(elementId)) return null;
            var node = grid.Node(elementId);
            sb.Append("<b>").Append(node.DisplayName).Append("</b>\n");
            switch (node.Kind)
            {
                case GridNodeKind.Load:
                    sb.Append("Tüketim: ").Append(node.Demand).Append('\n');
                    sb.Append("Durum: ").Append(LoadState(node, evaluation));
                    if (!string.IsNullOrEmpty(node.Detail)) sb.Append("\n<color=#c9d3df>").Append(node.Detail).Append("</color>");
                    if (interactive)
                        sb.Append("\n<color=#7fe0ff>Tıkla: ").Append(node.Enabled ? "yükü at (devreden çıkar)" : "yükü geri ver").Append("</color>");
                    break;
                default:
                    sb.Append("Altındaki toplam yük: ").Append(evaluation.LoadBelow(node.Id));
                    break;
            }
            return sb.ToString();
        }

        public static string LineState(GridLine line, GridEvaluation evaluation)
        {
            if (line.Faulted) return "<color=#ff5a4a>Arızalı</color>";
            if (line.Tripped) return "<color=#ff5a4a>Sigorta attı</color>";
            if (!line.Closed) return "Açık (enerji geçmiyor)";
            if (!evaluation.IsEnergized(line.FromId) || !evaluation.IsEnergized(line.ToId)) return "Kapalı ama enerjisiz";
            return "Kapalı (enerji iletiyor)";
        }

        public static string LoadState(GridNode node, GridEvaluation evaluation)
        {
            if (!node.Enabled) return "<color=#ff8a7a>Devre dışı (yük atıldı)</color>";
            return evaluation.IsPowered(node.Id) ? "<color=#ffe08a>Enerjili</color>" : "<color=#8a96a6>Karanlıkta</color>";
        }
    }
}
