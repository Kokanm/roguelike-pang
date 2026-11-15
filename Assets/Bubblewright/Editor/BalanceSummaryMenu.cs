using UnityEditor;
using UnityEngine;

namespace Bubblewright.EditorTools
{
    /// <summary>Bubblewright > Log balance summary (100 seeds): every normal stage slot averaged over 100 calm runs, to the Console.</summary>
    public static class BalanceSummaryMenu
    {
        [MenuItem("Bubblewright/Log balance summary (100 seeds)")]
        static void LogSummary() => Debug.Log(StageReport.Summary(100));
    }
}
