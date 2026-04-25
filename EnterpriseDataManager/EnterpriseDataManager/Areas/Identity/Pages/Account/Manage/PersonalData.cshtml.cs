// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace EnterpriseDataManager.Areas.Identity.Pages.Account.Manage
{
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<PersonalDataModel> _logger;

        public PersonalDataModel(UserManager<IdentityUser> userManager, ILogger<PersonalDataModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            _logger.LogInformation("User '{UserId}' downloaded their personal data.", _userManager.GetUserId(User));

            var data = new Dictionary<string, string>();

            foreach (var prop in typeof(IdentityUser).GetProperties()
                .Where(p => System.Attribute.IsDefined(p, typeof(PersonalDataAttribute))))
            {
                data[prop.Name] = prop.GetValue(user)?.ToString() ?? "null";
            }

            foreach (var login in await _userManager.GetLoginsAsync(user))
                data[$"{login.LoginProvider} external login key"] = login.ProviderKey;

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (!string.IsNullOrEmpty(authenticatorKey))
                data["AuthenticatorKey"] = authenticatorKey;

            Response.Headers["Content-Disposition"] = "attachment; filename=PersonalData.json";
            return new FileContentResult(
                JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true }),
                "application/json");
        }
    }
}
