using DiagnoseService.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactoryService.DataAccess
{
    public class DiagnoseContext : DbContext
    {
        public DiagnoseContext(DbContextOptions options) : base(options) { }
        public DbSet<Diagnoses> diagnoses { get; set; }
    }
}
