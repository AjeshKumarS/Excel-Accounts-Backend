using System.Collections.Generic;
using System.Threading.Tasks;
using API.Dtos.Ambassador;
using API.Models;

namespace API.Data.Interfaces
{
    public interface IAmbassadorRepository
    {
        Task<AmbassadorProfileDto> GetAmbassador(int id);
        Task<Ambassador> SignUpForAmbassador(int id);
        Task<User> ApplyReferralCode(int id, int referralCode);
        Task<List<UserViewDto>> ListOfReferredUsers(int id);
        Task<List<AmbassadorListViewDto>> ListOfAmbassadors();

    }
}