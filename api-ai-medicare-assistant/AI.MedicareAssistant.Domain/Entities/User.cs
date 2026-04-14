using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

public class User : BaseEntity
{
    [Required, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(20)]
    public string Phone { get; set; } = "";

    [Required, MaxLength(512)]
    public string PasswordHash { get; set; } = "";

    // Navigation properties
    public Profile? Profile { get; set; }
}
