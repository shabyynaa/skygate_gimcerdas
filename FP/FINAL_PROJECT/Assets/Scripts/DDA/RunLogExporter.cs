using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using SkyGate.Core;

namespace SkyGate.DDA
{
    public class RunLogExporter : MonoBehaviour
    {
        private string lastExportedFilePath = "";
        public string LastExportedFilePath => lastExportedFilePath;

        /// <summary>
        /// Exports the run telemetry & DDA adaptation decisions to CSV (GDD Section 10.2).
        /// </summary>
        public string ExportRunLog(
            int runSeed,
            bool isAdaptive,
            bool victory,
            IReadOnlyList<RoomTelemetrySummary> summaries,
            IReadOnlyList<DDADecisionRecord> decisions)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("RunSeed,IsAdaptive,Victory,Depth,IslandId,IslandType,ClearTimeSec,HPRatioStart,HPRatioEnd,DamageTaken,DamageDealt,DodgeSuccesses,StunHits,DeathsInRoom,GateChosen,SkillScore,SmoothedSkill,Confidence,PlaystyleLabel,DifficultyMultiplier,AdaptationReason");

                int count = summaries != null ? summaries.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    RoomTelemetrySummary s = summaries[i];
                    DDADecisionRecord d = (decisions != null && i < decisions.Count)
                        ? decisions[i]
                        : new DDADecisionRecord
                        {
                            depth = s.depth,
                            skillScore = 0.55f,
                            smoothedSkill = 0.55f,
                            confidence = 0.25f,
                            playstyleLabel = "Balanced",
                            difficultyMultiplier = 1.0f,
                            adaptationReason = "N/A"
                        };

                    sb.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6:F2},{7:F2},{8:F2},{9:F1},{10:F1},{11},{12},{13},{14},{15:F3},{16:F3},{17:F2},{18},{19:F2},\"{20}\"\n",
                        runSeed,
                        isAdaptive ? "Adaptive" : "Static",
                        victory ? 1 : 0,
                        s.depth,
                        s.islandId,
                        s.islandType,
                        s.clearTimeSeconds,
                        s.hpRatioStart,
                        s.hpRatioEnd,
                        s.damageTaken,
                        s.damageDealt,
                        s.dodgeSuccesses,
                        s.stunHits,
                        s.deathsInRoom,
                        s.gateChosen,
                        d.skillScore,
                        d.smoothedSkill,
                        d.confidence,
                        d.playstyleLabel,
                        d.difficultyMultiplier,
                        d.adaptationReason
                    );
                }

                string modeTag = isAdaptive ? "Adaptive" : "Static";
                string fileName = $"RunLog_{runSeed}_{modeTag}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                string persistentDir = Path.Combine(Application.persistentDataPath, "TelemetryLogs");
                Directory.CreateDirectory(persistentDir);
                string fullPath = Path.Combine(persistentDir, fileName);
                File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

                // Also write to project-local TelemetryLogs folder for immediate inspection
                string projectDir = Path.Combine( Path.GetDirectoryName(Application.dataPath) ?? ".", "TelemetryLogs");
                Directory.CreateDirectory(projectDir);
                string projectCopyPath = Path.Combine(projectDir, $"RunLog_{runSeed}.csv");
                File.WriteAllText(projectCopyPath, sb.ToString(), Encoding.UTF8);

                lastExportedFilePath = projectCopyPath;
                Debug.Log($"[SkyGate Telemetry] Exported Run CSV to: {projectCopyPath}");
                return projectCopyPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkyGate Telemetry] Failed to export CSV: {ex.Message}");
                return "";
            }
        }
    }
}
