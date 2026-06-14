
namespace Oloraculo.Web.Models.ApiFootballModels
{
    public class ApiTeams
    {
        public ApiTeam Home { get; set; } = new();
        public ApiTeam Away { get; set; } = new();
    }
}
