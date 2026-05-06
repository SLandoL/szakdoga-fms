using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace DiagnoseDashboard.Data
{
    public class FaultSearch
    {
        private ApplicationDbContext db;

        public static bool Led = false;

        public List<FaultData> faultDatas = new List<FaultData>();

        // --- LEVEL 1: Rendszer (30) ---
        public static FaultData KommRendszer { get; set; } = new FaultData(FaultPriority.MostCriticalProblem, "KommRendszer"); // 30
        public static FaultData AramRendszer { get; set; } = new FaultData(FaultPriority.MostCriticalProblem, "AramRendszer"); // 30

        // --- LEVEL 2: Központ (29) ---
        public static FaultData KommKozpont { get; set; } = new FaultData(FaultPriority.CriticalProblem, "KommKozpont"); // 29
        public static FaultData KommKozpontUp { get; set; } = new FaultData(FaultPriority.CriticalProblem, "KommKozpontUp"); // 29
        public static FaultData AramKommKozpont { get; set; } = new FaultData(FaultPriority.CriticalProblem, "AramKommKozpont"); // 29

        // --- LEVEL 3: Eszközök (28) - Párhuzamos RootFault lehetőség ---
        public static FaultData KommKocsi { get; set; } = new FaultData(FaultPriority.VeryHardFailure, "KommKocsi"); // 28
        public static FaultData KommTartaly { get; set; } = new FaultData(FaultPriority.VeryHardFailure, "KommTartaly"); // 28
        public static FaultData KommRfidUp { get; set; } = new FaultData(FaultPriority.VeryHardFailure, "KommRfidUp"); // 28
        public static FaultData GyarRfidOlv { get; set; } = new FaultData(FaultPriority.VeryHardFailure, "GyarRfidOlv"); // 28

        // --- LEVEL 4: Gyermekek (27 vagy kisebb) ---
        // Kocsi gyermekei
        public static FaultData KommKocsiEsp { get; set; } = new FaultData(FaultPriority.HardFailure, "KommKocsiEsp"); // 27
        public static FaultData GyarTargoncaSzenz { get; set; } = new FaultData(FaultPriority.HardFailure, "GyarTargoncaSzenz"); // 27
        public static FaultData KommTargoncaArammero { get; set; } = new FaultData(FaultPriority.HardFailure, "KommTargoncaArammero"); // 27
        public static FaultData AramKocsi { get; set; } = new FaultData(FaultPriority.HardFailure, "AramKocsi"); // 27

        // Tartály gyermekei
        public static FaultData GyarSzalagSzenz { get; set; } = new FaultData(FaultPriority.HardFailure, "GyarSzalagSzenz"); // 27
        public static FaultData GyarTartalySzenz { get; set; } = new FaultData(FaultPriority.HardFailure, "GyarTartalySzenz"); // 27
        public static FaultData AramTartaly { get; set; } = new FaultData(FaultPriority.HardFailure, "AramTartaly"); // 27
        public static FaultData KommTartalyArammero { get; set; } = new FaultData(FaultPriority.HardFailure, "KommTartalyArammero"); // 27
        public static FaultData KommKommArammero { get; set; } = new FaultData(FaultPriority.HardFailure, "KommKommArammero"); // 27

        public FaultSearch(ApplicationDbContext _db)
        {
            db = _db;
            faultDatas = db.faultDatas.Where(db => db.Valid == true).ToList();

            // --- HIERARCHIA ÉS PRIORITÁS SZINKRONIZÁLÁSA ---
            // Mivel az adatbázisban lévő értékek eltérhetnek, itt kényszerítjük a kód szerinti logikát.

            UpdateFaultData(KommRendszer, 0); // Top level, no parent
            UpdateFaultData(AramRendszer, 0);

            // Központnak a szülője a Rendszer (Priority 30)
            int level1Priority = (int)FaultPriority.MostCriticalProblem;
            UpdateFaultData(KommKozpont, level1Priority);
            UpdateFaultData(KommKozpontUp, level1Priority);
            UpdateFaultData(AramKommKozpont, level1Priority);

            // Eszközöknek a szülője a Központ (Priority 29)
            int level2Priority = (int)FaultPriority.CriticalProblem;
            UpdateFaultData(KommKocsi, level2Priority);
            UpdateFaultData(KommTartaly, level2Priority);
            UpdateFaultData(KommRfidUp, level2Priority);
            UpdateFaultData(GyarRfidOlv, level2Priority); // Párhuzamosan a KommRfidUp-al

            // Gyermekeknek a szülője az Eszköz (Priority 28)
            int level3Priority = (int)FaultPriority.VeryHardFailure;
            // Kocsi alattiak
            UpdateFaultData(KommKocsiEsp, level3Priority);
            UpdateFaultData(GyarTargoncaSzenz, level3Priority);
            UpdateFaultData(KommTargoncaArammero, level3Priority);
            UpdateFaultData(AramKocsi, level3Priority);
            // Tartály alattiak
            UpdateFaultData(GyarSzalagSzenz, level3Priority);
            UpdateFaultData(GyarTartalySzenz, level3Priority);
            UpdateFaultData(AramTartaly, level3Priority);
            UpdateFaultData(KommTartalyArammero, level3Priority);
            UpdateFaultData(KommKommArammero, level3Priority);
        }

        private void UpdateFaultData(FaultData reference, int parentPriority)
        {
            var item = faultDatas.FirstOrDefault(x => x.Name == reference.Name);
            if (item != null)
            {
                item.Priority = reference.Priority;
                if (parentPriority > 0)
                {
                    item.ParentId = parentPriority;
                }
            }
        }

        [Obsolete("Use RootCauseAnalyzer.DetectRootCauses instead. This old method keeps the previous priority-based root fault selection only for backward compatibility.")]
        public FaultPriority RootFaultDetectIndex()
        {
            Led = false;
            FaultPriority maxPriority = 0;
            foreach (FaultData item in faultDatas)
            {
                if (item.FaultStatus == FaultStatus.ROOTFAULT || item.FaultStatus == FaultStatus.FAULT && item.Priority > maxPriority)
                {
                    
                    maxPriority = item.Priority;
                }
            }
            if (maxPriority != 0)
            {
                Led = true;
            }
            Console.WriteLine("LED: " + Led);
            return maxPriority;
        }

        [Obsolete("Use RootCauseAnalyzer.DetectRootCauses instead. This old method marks every fault with the selected priority as ROOTFAULT and does not use the explicit fault hierarchy.")]
        public void RootFaultDetect(FaultPriority maxPriority)
        {
            foreach (FaultData item in faultDatas)
            {
                if (item.Priority == maxPriority)
                {
                    item.FaultStatus = FaultStatus.ROOTFAULT;
                }
            }
        }
    } 
}
