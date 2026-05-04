using DiagnoseDashboard.Data;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;



namespace DiagnoseDashboard.ViewModels
{
    public class DashboardViewModel
    {
        private DiagnoseDashboardService diagnoseDashboardService;

        public DashboardViewModel(DiagnoseDashboardService DiagnoseDashboardService)
        {
            diagnoseDashboardService = DiagnoseDashboardService;
        }

        public async Task GetDiagnoses()
        {
            await diagnoseDashboardService.GetDiagnosesAsync();
        }

        public async Task PostLedRed()
        {
            await diagnoseDashboardService.LedFailure();
        }
    }
}
