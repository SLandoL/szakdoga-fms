using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseDashboard.Data
{
    public enum FaultPriority
    {
        MostCriticalProblem = 30,
        CriticalProblem = 29,
        VeryHardFailure = 28,
        HardFailure = 27,
        HighMediumFailure = 26,
        MediumFailure = 25,
        LowMediumFailure = 24,
        HighEasyFailure = 23,
        EasyFailure = 22,
        LowEasyFailure = 21,
        NoneCriticalProblem = 20,
        DummyFailure19 = 19,
        DummyFailure18 = 18,
        DummyFailure17 = 17,
        DummyFailure16 = 16,
        DummyFailure15 = 15,
        DummyFailure14 = 14,
        DummyFailure13 = 13,
        DummyFailure12 = 12,
        DummyFailure11 = 11,
        DummyFailure10 = 10,
    }
}
