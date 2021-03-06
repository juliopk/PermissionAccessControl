﻿// Copyright (c) 2018 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataLayer.EfClasses;
using DataLayer.EfCode;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PermissionParts;
using TestWebApp.Data;

namespace TestWebApp.RolesToPermissions
{
    public static class StartupExtensions
    {
        public const string StaffRoleName = "Staff";
        public const string ManagerRoleName = "Manager";
        public const string AdminRoleName = "Admin";

        public const string StaffUserEmail = "Staff@g1.com";
        public const string ManagerUserEmail = "Manager@g1.com";
        public const string AdminUserEmail = "Admin@g1.com";

        private static readonly List<RoleToPermissions> DefaultRoles = new List<RoleToPermissions>
        {
            new RoleToPermissions(StaffRoleName, "Staff can only read data", new List<Permissions>{ Permissions.ColorRead, Permissions.Feature1Access}),
            new RoleToPermissions(ManagerRoleName, "Managers can read/write the data", 
                new List<Permissions>{ Permissions.ColorRead, Permissions.ColorCreate, Permissions.ColorDelete, Permissions.ColorUpdate, Permissions.Feature1Access}),
            new RoleToPermissions(AdminRoleName, "Admin can manage users, but not read data",
                new List<Permissions> {Permissions.UserRead,Permissions.UserChange, Permissions.Feature1Access, Permissions.Feature2Access }),
        };

        private static readonly List<ModulesForUser> DefaultModules = new List<ModulesForUser>
        {
            new ModulesForUser(StaffUserEmail, PaidForModules.Feature1),
            //Note that there is no entry for ManagerUserEmail - that means they can't access any feature modules
            new ModulesForUser(AdminUserEmail, PaidForModules.Feature1 | PaidForModules.Feature2),
        };

        //NOTE: ShortName must be an email
        private static readonly List<IdentityUser> DefaultUsers = new List<IdentityUser>
        {
            new IdentityUser{ UserName = StaffUserEmail, Email = StaffUserEmail},
            new IdentityUser{ UserName = ManagerUserEmail, Email = ManagerUserEmail},
            new IdentityUser{ UserName = AdminUserEmail, Email = AdminUserEmail},
        };

        public static void SetupDatabases(this IWebHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                using (var context = services.GetRequiredService<ExtraAuthorizeDbContext>())
                {
                    context.Database.EnsureCreated();
                    context.AddRange(DefaultRoles);
                    context.AddRange(DefaultModules);
                    context.SaveChanges();
                }
                using (var context = services.GetRequiredService<ApplicationDbContext>())
                {
                    context.Database.EnsureCreated();
                }
            }
        }

        public static async Task SetupDefaultUsersAsync(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                await AddUserWithRoles(DefaultUsers[0], userManager, roleManager, StaffRoleName);
                await AddUserWithRoles(DefaultUsers[1], userManager, roleManager, ManagerRoleName);
                await AddUserWithRoles(DefaultUsers[2], userManager, roleManager, ManagerRoleName, AdminRoleName);
            }
        }


        //---------------------------------------------------------------------------
        //private methods


        private static async Task AddUserWithRoles(IdentityUser user, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager,
            params string [] roleNames)
        {
            var result = await userManager.CreateAsync(user, user.Email); //email is the password
            if (!result.Succeeded)
                throw new InvalidOperationException($"Tried to add user {user.UserName}, but failed.");

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    //create the roles and seed them to the database: Question 1
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
                await userManager.AddToRoleAsync(user, roleName);
            }
        }
    }
}