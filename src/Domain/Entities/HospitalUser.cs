using System;
using System.Collections.Generic;
using System.Text;

namespace AfriHospital.src.Domain.Entities
{
    public class HospitalUser
    {
        public Guid Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
