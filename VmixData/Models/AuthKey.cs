
using System;
using System.ComponentModel.DataAnnotations;

namespace VmixData.Models
{
    public class AuthKey
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string KeyValue { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
