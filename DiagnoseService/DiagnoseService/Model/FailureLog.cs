using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnoseService.Model
{
    public class FailureLog
    {
        public int Id { get; set; }

        //Hibaforrás
        [Required]
        [MaxLength(300)]
        public string Fail { get; set; }
        [Required]
        public DateTime Date { get; set; }
    }
}
