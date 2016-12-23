﻿using System.ComponentModel.DataAnnotations;

namespace Epi.Web.MVC.Models
{
    public class UserLoginModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [RegularExpression(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", ErrorMessage = "Invalid email address.")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }

        public bool ViewValidationSummary { get; set; }
    }
}