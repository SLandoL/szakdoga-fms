using DiagnoseService.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseService.DataAccess
{
    public class FailureContext : DbContext
    {
        public FailureContext(DbContextOptions options) : base(options) { }
        public DbSet<FailureLog> failureLogs { get; set; }
    }
}
