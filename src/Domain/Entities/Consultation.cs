using System;
using System.Collections.Generic;
using System.Text;

namespace AfriHospital.src.Domain.Entities
{
    public class Consultation
    {
        public Guid Id { get; set; }

        public Guid PatientId { get; set; }

        public Guid PractitionerId { get; set; }

        public DateTime ConsultationDateUtc { get; set; }

        public string? ClinicalNotes { get; set; }

        public string? Diagnosis { get; set; }
    }
}
