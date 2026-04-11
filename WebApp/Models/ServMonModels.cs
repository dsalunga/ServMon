using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace ServMonWeb.Models
{
    public class EditConfigViewModel
    {
        [Required]
        [Display(Name = "Content")]
        public string Content { get; set; }
    }
}