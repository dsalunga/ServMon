using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ServMonWebV4.Models
{
    public class EditConfigViewModel
    {
        [Required]
        [Display(Name = "Content")]
        public string Content { get; set; }
    }
}