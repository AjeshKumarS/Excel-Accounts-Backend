using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using API.Data.Interfaces;
using API.Dtos.Auth;
using API.Models;
using API.Services.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;
        private readonly IAuthRepository _repo;
        private readonly HttpClient _httpClient;
        private readonly IQRCodeGeneration _qRCodeGeneration;

        public AuthService(IMapper mapper, IConfiguration config, IAuthRepository repo, HttpClient httpClient, IQRCodeGeneration qRCodeGeneration)
        {
            _qRCodeGeneration = qRCodeGeneration;
            _mapper = mapper;
            _config = config;
            _repo = repo;
            _httpClient = httpClient;
        }

        public async Task<string> CreateJwtForClient(string responseString)
        {
            var userFromAuth0 = JsonConvert.DeserializeObject<UserFromAuth0Dto>(responseString);
            if (!await _repo.UserExists(userFromAuth0.email))
            {
                var newUser = _mapper.Map<User>(userFromAuth0);
                await _repo.Register(newUser);
                newUser.QRCodeUrl = await _qRCodeGeneration.CreateQrCode(newUser.Id.ToString());
            }
            User user = await _repo.GetUser(userFromAuth0.email);
            var claims = new[] {
                new Claim("user_id", user.Id.ToString()),
                new Claim("email", user.Email),
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(30),
                SigningCredentials = creds,
                Issuer = _config.GetSection("AppSettings:Issuer").Value
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<string> FetchUserFromAuth0(string auth_token)
        {
            var url = new Uri(_config.GetSection("AppSettings:Auth0Server").Value);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", auth_token);
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Authorization header has been set, but the server reports that it is missing.
                // It was probably stripped out due to a redirect.

                var finalRequestUri =
                    response.RequestMessage.RequestUri; // contains the final location after following the redirect.

                if (finalRequestUri != url) // detect that a redirect actually did occur.
                {
                    // If this is public facing, add tests here to determine if Url should be trusted
                    response = await _httpClient.GetAsync(finalRequestUri);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
            }
            return response.Content.ReadAsStringAsync().Result;
        }
    }
}