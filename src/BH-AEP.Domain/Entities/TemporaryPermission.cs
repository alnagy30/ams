namespace BH_AEP.Domain.Entities;

public class TemporaryPermission : BaseEntity
{
    public int UserId { get; set; }
    public string Permission { get; set; } = "";
    public int GrantedBy { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsRevoked { get; set; }

    public User? User { get; set; }
}
