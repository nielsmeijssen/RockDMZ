using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using RockDMZ.Infrastructure;
using Microsoft.Extensions.Options;

namespace RockDMZ.Features.ServiceAccount
{
    public class ServiceAccountController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ServicesContext _servicesContext;
        private readonly FileStorage _fileStorage;

        public ServiceAccountController(IMediator mediator, IOptions<ServicesContext> servicesContext, IOptions<FileStorage> fileStorage)
        {
            _mediator = mediator;
            _servicesContext = servicesContext.Value;
            _fileStorage = fileStorage.Value;
        }

        public async Task<IActionResult> Index(Index.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model); 
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Create.Command command)
        {
            try
            {
                if (command.ServiceName == Domain.ServiceName.GoogleAnalytics)
                {
                    command.JsonSecret = _servicesContext.GoogleAnalytics.JsonSecret;
                    command.CredentialsDirectory = _fileStorage.Credentials;
                }
                
                await _mediator.Send(command);

                return this.RedirectToActionJson(nameof(Index));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError(ex.Message, ex.InnerException.ToString());
                return View();
            }
        }

        public async Task<IActionResult> Details(Details.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model);
        }

        public async Task<IActionResult> Edit(Edit.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Edit.Command command)
        {
            await _mediator.Send(command);

            return this.RedirectToActionJson(nameof(Index));
        }

        public async Task<IActionResult> Delete(Delete.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Delete.Command command)
        {
            await _mediator.Send(command);

            return this.RedirectToActionJson(nameof(Index));
        }
    }
}