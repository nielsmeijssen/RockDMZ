﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using RockDMZ.Infrastructure;
using Microsoft.Extensions.Options;

namespace RockDMZ.Features.AdWordsLocation
{
    public class AdWordsLocationController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ServicesContext _servicesContext;
        private readonly FileStorage _fileStorage;

        public AdWordsLocationController(IMediator mediator, IOptions<ServicesContext> servicesContext, IOptions<FileStorage> fileStorage)
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

        public async Task<IActionResult> Create(Create.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Create.Command command)
        {
            await _mediator.Send(command);

            return this.RedirectToActionJson(nameof(Index));
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

            return this.RedirectToActionJson(nameof(EditLaunch), new { Id = command.Id });
        }

        public async Task<IActionResult> EditLaunch(EditLaunch.Query query)
        {
            var model = await _mediator.Send(query);

            return View(model);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> EditLaunch(EditLaunch.Command command)
        //{
        //    await _mediator.Send(command);

        //    return this.RedirectToActionJson(nameof(Index));
        //}
    }
}
