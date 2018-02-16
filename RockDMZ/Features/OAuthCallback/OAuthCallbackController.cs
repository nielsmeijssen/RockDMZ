using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2.Mvc;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using RockDMZ.Infrastructure;
using Microsoft.Extensions.Options;

namespace RockDMZ.Features.OAuthCallback
{
    public class OAuthCallbackController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ServicesContext _servicesContext;

        public OAuthCallbackController(IMediator mediator, IOptions<ServicesContext> servicesContext)
        {
            _mediator = mediator;
            _servicesContext = servicesContext.Value;
        }

        public async Task<IActionResult> Index(Index.Query query)
        {
            var model = await _mediator.Send(query);

            return RedirectToAction("Index","ServiceAccount");
        }
    }
}