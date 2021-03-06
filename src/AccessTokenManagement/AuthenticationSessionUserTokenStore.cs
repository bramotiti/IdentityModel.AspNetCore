﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IdentityModel.AspNetCore.AccessTokenManagement
{
    /// <summary>
    /// Token store using the ASP.NET Core authentication session
    /// </summary>
    public class AuthenticationSessionUserTokenStore : IUserTokenStore
    {
        private const string TokenPrefix = ".Token.";
            
        private readonly IHttpContextAccessor _contextAccessor;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="contextAccessor"></param>
        public AuthenticationSessionUserTokenStore(
            IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        /// <inheritdoc/>
        public async Task<UserAccessToken> GetTokenAsync(ClaimsPrincipal user, string resource)
        {
            var result = await _contextAccessor.HttpContext.AuthenticateAsync();

            if (!result.Succeeded)
            {
                return null;
            }

            var tokens = result.Properties.Items.Where(i => i.Key.StartsWith(TokenPrefix)).ToList();
            if (tokens == null || !tokens.Any())
            {
                throw new InvalidOperationException(
                    "No tokens found in cookie properties. SaveTokens must be enabled for automatic token refresh.");
            }

            string tokenName = $"{TokenPrefix}{OpenIdConnectParameterNames.AccessToken}";
            if (!string.IsNullOrEmpty(resource))
            {
                tokenName += $"::{resource}";
            }

            string expiresName = $"{TokenPrefix}expires_at";
            if (!string.IsNullOrEmpty(resource))
            {
                expiresName += $"::{resource}";
            }

            var accessToken = tokens.SingleOrDefault(t => t.Key == tokenName);
            var refreshToken = tokens.SingleOrDefault(t => t.Key == $"{TokenPrefix}{OpenIdConnectParameterNames.RefreshToken}");
            var expiresAt = tokens.SingleOrDefault(t => t.Key == expiresName);

            DateTimeOffset? dtExpires = null;
            if (expiresAt.Value != null)
            {
                dtExpires = DateTimeOffset.Parse(expiresAt.Value, CultureInfo.InvariantCulture);
            }

            return new UserAccessToken
            {
                AccessToken = accessToken.Value,
                RefreshToken = refreshToken.Value,
                Expiration = dtExpires
            };
        }

        /// <inheritdoc/>
        public async Task StoreTokenAsync(ClaimsPrincipal user, string accessToken, DateTimeOffset expiration,
            string refreshToken = null, string resource = null)
        {
            var result = await _contextAccessor.HttpContext.AuthenticateAsync();

            if (!result.Succeeded)
            {
                throw new Exception("Can't store tokens. User is anonymous");
            }

            string tokenName = OpenIdConnectParameterNames.AccessToken;
            if (!string.IsNullOrEmpty(resource))
            {
                tokenName += $"::{resource}";
            }

            string expiresName = "expires_at";
            if (!string.IsNullOrEmpty(resource))
            {
                expiresName += $"::{resource}";
            }


            result.Properties.Items[$".Token.{tokenName}"] = accessToken;
            result.Properties.Items[$".Token.{expiresName}"] = expiration.ToString("o", CultureInfo.InvariantCulture);

            if (refreshToken != null)
            {
                result.Properties.UpdateTokenValue(OpenIdConnectParameterNames.RefreshToken, refreshToken);
            }

            await _contextAccessor.HttpContext.SignInAsync(result.Principal, result.Properties);
        }

        /// <inheritdoc/>
        public Task ClearTokenAsync(ClaimsPrincipal user)
        {
            return Task.CompletedTask;
        }
    }
}