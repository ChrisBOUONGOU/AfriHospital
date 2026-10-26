using System;
using System.Collections.Generic;
using System.Text;

namespace AfriHospital.src.Domain.Entities
{
    public class Patient
    {
        public Guid Id { get; set; }
        public string PatientNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
