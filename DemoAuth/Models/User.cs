using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace DemoAuth.Models
{
    public partial class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Sdt { get; set; }
        public string TokenOtp { get; set; }
        public bool? Active { get; set; }
        public int? Amount { get; set; }

        [NotMapped]
        public virtual string Otp { get; set; }
    }
}
