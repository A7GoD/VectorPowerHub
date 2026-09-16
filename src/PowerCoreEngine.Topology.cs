using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    public CpuTopology Topology { get; private set; }

    private void BuildTopology() {
        CpuTopology topology = new CpuTopology();
        bool success = false;

        try {
            uint length = 0;
            Win32Native.GetLogicalProcessorInformationEx(
                Win32Native.LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore,
                IntPtr.Zero,
                ref length);

            if (length > 0) {
                IntPtr buffer = Marshal.AllocHGlobal((int)length);
                try {
                    if (Win32Native.GetLogicalProcessorInformationEx(
                        Win32Native.LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore,
                        buffer,
                        ref length)) {

                        Dictionary<byte, List<CpuCore>> coresByEff = new Dictionary<byte, List<CpuCore>>();
                        int offset = 0;
                        int coreCount = 0;

                        while (offset < length) {
                            IntPtr cur = new IntPtr(buffer.ToInt64() + offset);
                            Win32Native.SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX info =
                                (Win32Native.SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX)Marshal.PtrToStructure(
                                    cur, typeof(Win32Native.SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX));

                            if (info.Size == 0) break;

                            if (info.Relationship == Win32Native.LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore) {
                                byte effClass = info.Processor.EfficiencyClass;
                                byte flags = info.Processor.Flags;
                                ulong mask = info.Processor.GroupMask.Mask.ToUInt64();

                                int logicalId = coreCount;
                                for (int b = 0; b < 64; b++) {
                                    if (((mask >> b) & 1UL) != 0UL) {
                                        logicalId = b;
                                        break;
                                    }
                                }

                                CpuCore core = new CpuCore(logicalId, effClass);
                                core.AffinityMask = mask;
                                core.Flags = flags;

                                List<CpuCore> list;
                                if (!coresByEff.TryGetValue(effClass, out list)) {
                                    list = new List<CpuCore>();
                                    coresByEff[effClass] = list;
                                }
                                list.Add(core);
                                coreCount++;
                            }

                            offset += (int)info.Size;
                        }

                        if (coreCount > 0) {
                            List<byte> effKeys = new List<byte>(coresByEff.Keys);
                            effKeys.Sort();
                            effKeys.Reverse(); // Higher efficiency class (P-cores) first

                            int clusterId = 0;
                            for (int i = 0; i < effKeys.Count; i++) {
                                byte eff = effKeys[i];
                                List<CpuCore> clusterCores = coresByEff[eff];
                                string clusterName;
                                if (effKeys.Count == 1) {
                                    clusterName = (eff > 0) ? "Performance Cores" : "Standard Cores";
                                } else if (effKeys.Count == 2) {
                                    clusterName = (eff > 0) ? "Performance Cores" : "Efficiency Cores";
                                } else {
                                    if (eff >= 2) clusterName = "Performance Cores";
                                    else if (eff == 1) clusterName = "Efficiency Cores";
                                    else clusterName = "Low Power Cores";
                                }

                                CpuCluster cluster = new CpuCluster(clusterId++, clusterName, eff);
                                cluster.Cores = clusterCores;
                                topology.Clusters.Add(cluster);
                            }

                            topology.TotalCores = coreCount;
                            ApplyHardwareDictionary(topology);
                            this.Topology = topology;
                            success = true;
                        }
                    }
                } finally {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        } catch {
            success = false;
        }

        if (!success || this.Topology == null || this.Topology.TotalCores == 0) {
            int fallbackCount = Environment.ProcessorCount;
            if (fallbackCount <= 0) fallbackCount = 1;

            CpuTopology fallbackTopology = new CpuTopology();
            CpuCluster cluster = new CpuCluster(0, "Standard Cores", 0);
            for (int i = 0; i < fallbackCount; i++) {
                CpuCore core = new CpuCore(i, 0);
                core.AffinityMask = (i < 64) ? (1UL << i) : 0UL;
                cluster.Cores.Add(core);
            }
            fallbackTopology.Clusters.Add(cluster);
            fallbackTopology.TotalCores = fallbackCount;
            ApplyHardwareDictionary(fallbackTopology);
            this.Topology = fallbackTopology;
        }
    }

    private void ApplyHardwareDictionary(CpuTopology topology) {
        if (topology == null || topology.Clusters == null) return;
        try {
            string vendor = string.Empty;
            string identifier = string.Empty;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
                if (key != null) {
                    vendor = (key.GetValue("VendorIdentifier") as string) ?? string.Empty;
                    identifier = (key.GetValue("Identifier") as string) ?? string.Empty;
                }
            }

            int family = 0;
            int model = 0;
            Match famMatch = Regex.Match(identifier, @"Family\s+(\d+)");
            if (famMatch.Success) int.TryParse(famMatch.Groups[1].Value, out family);
            Match modMatch = Regex.Match(identifier, @"Model\s+(\d+)");
            if (modMatch.Success) int.TryParse(modMatch.Groups[1].Value, out model);

            string pArch = "Unknown";
            string eArch = "N/A";

            if (vendor == "GenuineIntel" && family == 6) {
                switch (model) {
                    case 0xC5: pArch = "Lion Cove"; eArch = "Skymont"; break;
                    case 0xC6: pArch = "Lion Cove"; eArch = "Skymont"; break;
                    case 0xBD: pArch = "Lion Cove"; eArch = "Skymont"; break;
                    case 0xAA: pArch = "Redwood Cove"; eArch = "Crestmont"; break;
                    case 0xAC: pArch = "Redwood Cove"; eArch = "Crestmont"; break;
                    case 0xB7: pArch = "Raptor Cove"; eArch = "Gracemont"; break;
                    case 0xBA: pArch = "Raptor Cove"; eArch = "Gracemont"; break;
                    case 0xBF: pArch = "Raptor Cove"; eArch = "Gracemont"; break;
                    case 0x97: pArch = "Golden Cove"; eArch = "Gracemont"; break;
                    case 0x9A: pArch = "Golden Cove"; eArch = "Gracemont"; break;
                    default: pArch = "Intel P-Core"; eArch = "Intel E-Core"; break;
                }
            } else if (vendor == "AuthenticAMD") {
                switch (family) {
                    case 0x1A:
                        pArch = "Zen 5 (Nirvana)";
                        eArch = (model >= 0x20 && model <= 0x2F) ? "Zen 5c (Prometheus)" : "N/A";
                        break;
                    case 0x19:
                        pArch = "Zen 4 (Persephone)";
                        eArch = (model >= 0x70 && model <= 0x7F) ? "Zen 4c (Dionysus)" : "N/A";
                        break;
                    case 0x17:
                        pArch = "Zen 2 / Zen 1";
                        break;
                }
            }

            if (pArch == "Unknown") return;

            for (int i = 0; i < topology.Clusters.Count; i++) {
                CpuCluster cluster = topology.Clusters[i];
                if (cluster.EfficiencyClass >= 1) {
                    cluster.Name = pArch;
                } else {
                    cluster.Name = (topology.Clusters.Count == 1 && eArch == "N/A") ? pArch : eArch;
                }
            }
        } catch {
        }
    }
}
