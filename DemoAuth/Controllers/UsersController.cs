using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoAuth.Models;
using System.Security.Cryptography;
using System.Text;
using OtpNet;
using System.Net.Http;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace DemoAuth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly DemoOtpContext _context;

        public UsersController(DemoOtpContext context)
        {
            _context = context;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        // PUT: api/Users/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Users
        // To protect from overposting attacks, enable the specific properties you want to bind to, for
        // more details, see https://go.microsoft.com/fwlink/?linkid=2123754.
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            user.TokenOtp = ComputeSha256Hash(user.Username + user.Sdt).Substring(0,15);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUser", new { id = user.Id }, user);
        }

        [HttpPost("CheckLogin")]
        public ActionResult<Object> CheckLogin(User user)
        {
            User u = _context.Users.Where(u => u.Username == user.Username && u.Password == user.Password).FirstOrDefault();
            if (u == null) 
                return new {Auth= false};
            return new { Auth = true, u };
        }

        string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // DELETE: api/Users/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<User>> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return user;
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }

        [HttpPost("getOtp")]
        public ActionResult<Object> getOTP(User user)
        {
            User u = _context.Users.Where(u => u.Username == user.Username).FirstOrDefault();
            if (u == null) return new { Auth = false };
            sendOTP(u.Sdt, u.TokenOtp);
            return new { Auth = true, Phone = u.Sdt, Id = u.Id };
        }
        public void sendOTP(string phone, string key)
        {
            var topt = new Totp(Encoding.ASCII.GetBytes(key), step: 60, totpSize: 5);
            string otp = topt.ComputeTotp(DateTime.Now);
            int i =1;
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/");
            HttpResponseMessage res = client.PostAsJsonAsync("", new
            {
                ApiKey = "99211B5D3B0EE6F69939FFB99BFD11",
                //"881A61A59E543FBA4B70F709D4354A",
                SecretKey = "AA5496014FCB8CEC813D5896453D67",
                //"739DFB9AA0FA885C6B9B5BFA934674",
                Phone = phone,
                SmsType = 2,
                Brandname = "Baotrixemay",
                Content = otp
            }).Result;
            Console.WriteLine(res);

        }

        

        [HttpPost("verifyOtp")]
        public ActionResult<Object> VerifyOTP(User user)
        {
            User u = _context.Users.Where(u => u.Id == user.Id).FirstOrDefault();
            if (u == null) return new { Auth = false };
            var topt = new Totp(Encoding.ASCII.GetBytes(u.TokenOtp), step: 60, totpSize: 5);
            string otp = topt.ComputeTotp(DateTime.Now);
            if(!otp.Equals(user.Otp)) return new { Auth = false };
            string newPassword = RandomString(6);
            u.Password = newPassword;
            _context.Entry(u).State = EntityState.Modified;
            _context.SaveChanges();
            return new { Auth = true, Phone = u.Sdt, Id = u.Id, NewPass = newPassword };
        }

        [Authorize]
        [HttpPost("SendMoney")]
        public async Task<Object> SendMoney(User user)
        {
            var idUser = User.Claims.FirstOrDefault(x => x.Type.Equals("ID", StringComparison.InvariantCultureIgnoreCase)).Value;
            User u = await _context.Users.FindAsync(Int32.Parse(idUser));
            if (u == null) return new { Auth = false, Message = "Vui lòng đăng nhập" };
            var topt = new Totp(Encoding.ASCII.GetBytes(u.TokenOtp), step: 60, totpSize: 5);
            string otp = topt.ComputeTotp(DateTime.Now);
            if (!otp.Equals(user.Otp)) return new { Auth = false, Message = "Sai mã Otp"};
            if(u.Amount < user.Amount) return new { Auth = false, Message = "Không đủ tiền gửi" };
            User u2 = _context.Users.Where(u => u.Username == user.Username).FirstOrDefault();
            if (u2 == null) return new { Auth = false, Message = "Tài khoản chuyển không tồn tại" };
            u.Amount = u.Amount - user.Amount;
            u2.Amount = u2.Amount + user.Amount;
            _context.Entry(u).State = EntityState.Modified;
            _context.Entry(u2).State = EntityState.Modified;
            _context.SaveChanges();
            return new { Auth = true, Message = "Chuyển tiền thành công" };
        }

        public string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpPost("getToken")]
        public IActionResult GetToken(User user)
        {
            User u = _context.Users.Where(u => u.Username == user.Username && user.Password == u.Password).FirstOrDefault();
            if (u == null)
            {
                return Ok(new
                {
                    Auth = false
                });
            }
            IList<Claim> claims = new List<Claim>();
            claims.Add(new Claim("Username", u.Username));
            claims.Add(new Claim("ID", u.Id.ToString()));
            // security key
            string securityKey = "this_is_super_long_security_key_for_token_validation_project_2018_09_07$smesk.in";

            // symmetric security key
            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));

            // signing credentials
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256Signature);

            // create token
            var token = new JwtSecurityToken(
                issuer: "https://localhost:44396/",
                audience: "https://localhost:44396/",
                expires: DateTime.Now.AddHours(1),
                signingCredentials: signingCredentials,
                claims: claims
            );

            // return token
            return Ok(new
            {
                Auth = true,
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }

        [HttpGet("MyUser")]
        [Authorize]
        public async Task<User> MyUser()
        {
            var idUser = User.Claims.FirstOrDefault(x => x.Type.Equals("ID", StringComparison.InvariantCultureIgnoreCase)).Value;
            User u = await _context.Users.FindAsync(Int32.Parse(idUser));
            return u;
        }


    }
}
