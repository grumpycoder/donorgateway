﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using CsvHelper;
using DonorGateway.Admin.Helpers;
using DonorGateway.Admin.ViewModels;
using DonorGateway.Data;
using DonorGateway.Domain;
using EntityFramework.Extensions;
using System;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Http;

#pragma warning disable 618

namespace DonorGateway.Admin.Controllers
{
    [RoutePrefix("api/event")]
    public class EventController : ApiController
    {
        private readonly DataContext _context;
        private const int PAGE_SIZE = 20;

        public EventController()
        {
            _context = DataContext.Create();
        }

        public async Task<object> Get()
        {
            var list = await _context.Events.OrderByDescending(e => e.CreatedDate).ProjectTo<EventSummaryViewModel>().ToListAsync();
            return Ok(list);
        }

        [Route("{name}")]
        public async Task<object> Get(string name)
        {
            var model = await _context.Events.FirstOrDefaultAsync(e => e.NameUrl == name);
            if (model == null) return BadRequest("Event not found");

            var @event = Mapper.Map<EventSummaryViewModel>(model);
            return Ok(@event);
        }

        [HttpGet, Route("{id:int}")]
        public async Task<object> Get(int id)
        {
            var @event = await _context.Events.SingleOrDefaultAsync(x => x.Id == id);
            if (@event == null) return NotFound();

            var attendanceCount = _context.Guests.Where(g => g.EventId == id && g.IsAttending == true && g.IsWaiting == false).Sum(g => g.TicketCount);
            @event.GuestAttendanceCount = attendanceCount ?? 0;
            var waitingCount = _context.Guests.Where(g => g.EventId == id && g.IsAttending == true && g.IsWaiting).Sum(g => g.TicketCount);
            @event.GuestWaitingCount = waitingCount ?? 0;
            @event.TicketMailedCount = _context.Guests.Where(g => g.EventId == id && g.IsAttending == true && g.IsMailed && !g.IsWaiting).Sum(g => g.TicketCount) ?? 0;
            _context.SaveChanges();

            var model = Mapper.Map<EventViewModel>(@event);
            return Ok(model);
        }

        public async Task<object> Put(EventViewModel model)
        {
            try
            {
                var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == model.Id);
                if (@event == null) return BadRequest("Event not found");

                var existingEvent =
                    await _context.Events.FirstOrDefaultAsync(
                        e => e.NameUrl == model.NameUrl && e.Id != model.Id && e.EndDate >= DateTime.Now);

                if (existingEvent != null)
                {
                    ModelState.AddModelError("Name", "Active event name already exists with this url name");
                    return BadRequest(ModelState);
                }


                Mapper.Map(model, @event);

                _context.Events.AddOrUpdate(@event);
                await _context.SaveChangesAsync();

                Mapper.Map(@event, model);

                return Ok(model);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        public async Task<object> Post(EventViewModel model)
        {
            try
            {
                var @event = await _context.Events.FirstOrDefaultAsync(e => e.NameUrl == model.NameUrl && e.EndDate >= DateTime.Now);
                if (@event != null) return BadRequest("Event name already exists");

                @event = Mapper.Map<Event>(model);
                @event.Template = new Template() { Name = model.NameUrl };
                _context.Events.AddOrUpdate(@event);
                await _context.SaveChangesAsync();
                Mapper.Map(@event, model);
                return Ok(model);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpDelete, Route("{id}")]
        public async Task<object> Delete(int id)
        {
            try
            {
                var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
                if (@event == null) return BadRequest("Event not found");

                var template = await _context.Templates.FirstOrDefaultAsync(e => e.Id == @event.TemplateId);
                _context.Templates.Remove(template);
                await _context.SaveChangesAsync();
                await _context.Events.DeleteAsync(e => e.Id == id);
                await _context.SaveChangesAsync();
                return Ok("Deleted Event");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet, Route("{id:int}/guests")]
        public async Task<object> Guests(int id, [FromUri]GuestSearchModel pager)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Restart();
            if (pager == null) pager = new GuestSearchModel();

            var ticketMailed = pager.IsMailed ?? false;
            var isWaiting = pager.IsWaiting ?? false;
            var isAttending = pager.IsAttending ?? false;
            var query = _context.Guests.Where(e => e.EventId == id);
            var totalCount = await query.CountAsync();

            var pred = PredicateBuilder.True<Guest>();
            if (!string.IsNullOrWhiteSpace(pager.Address)) pred = pred.And(p => p.Address.Contains(pager.Address));
            if (!string.IsNullOrWhiteSpace(pager.FinderNumber)) pred = pred.And(p => p.FinderNumber.StartsWith(pager.FinderNumber));
            if (!string.IsNullOrWhiteSpace(pager.Name)) pred = pred.And(p => p.Name.Contains(pager.Name));
            if (!string.IsNullOrWhiteSpace(pager.City)) pred = pred.And(p => p.City.StartsWith(pager.City));
            if (!string.IsNullOrWhiteSpace(pager.State)) pred = pred.And(p => p.State.Equals(pager.State));
            if (!string.IsNullOrWhiteSpace(pager.ZipCode)) pred = pred.And(p => p.Zipcode.StartsWith(pager.ZipCode));
            if (!string.IsNullOrWhiteSpace(pager.Phone)) pred = pred.And(p => p.Phone.Contains(pager.Phone));
            if (!string.IsNullOrWhiteSpace(pager.Email)) pred = pred.And(p => p.Email.StartsWith(pager.Email));
            if (!string.IsNullOrWhiteSpace(pager.LookupId)) pred = pred.And(p => p.LookupId.StartsWith(pager.LookupId));
            if (!string.IsNullOrWhiteSpace(pager.ConstituentType)) pred = pred.And(p => p.ConstituentType.StartsWith(pager.ConstituentType));
            if (pager.IsMailed != null) pred = pred.And(p => p.IsMailed == ticketMailed);
            if (pager.IsWaiting != null) pred = pred.And(p => p.IsWaiting == isWaiting);
            if (pager.IsAttending != null) pred = pred.And(p => p.IsAttending == isAttending);

            var filteredQuery = query.Where(pred);
            var pagerCount = await filteredQuery.CountAsync();
            var totalPages = Math.Ceiling((double)pagerCount / pager.PageSize ?? PAGE_SIZE);

            var results = await query.Where(pred)
                .Order(pager.OrderBy, pager.OrderDirection == "desc" ? SortDirection.Descending : SortDirection.Ascending)
                .Skip(pager.PageSize * (pager.Page - 1) ?? 0)
                .Take(pager.PageSize ?? PAGE_SIZE)
                .ProjectTo<GuestViewModel>().ToListAsync();

            pager.TotalCount = totalCount;
            pager.FilteredCount = pagerCount;
            pager.TotalPages = totalPages;
            pager.Results = results;
            stopwatch.Stop();
            pager.ElapsedTime = stopwatch.Elapsed;
            return Ok(pager);
        }

        [HttpPost, Route("{id:int}/register")]
        public async Task<object> RegisterGuest(int id, [FromBody]GuestViewModel model)
        {
            var @event = await _context.Events.SingleOrDefaultAsync(e => e.Id == id);
            if (@event == null) return BadRequest("Event not found");

            var guest = await _context.Guests.FirstOrDefaultAsync(e => e.Id == model.Id);
            if (guest == null) guest = Mapper.Map<Guest>(model);

            var dto = Mapper.Map<Guest>(model);
            if (guest != dto) await CreateDemographicRecord(dto);

            Mapper.Map(model, guest);
            if (string.IsNullOrWhiteSpace(guest.InsideSalutation)) guest.InsideSalutation = guest.Name;

            @event.RegisterGuest(guest);
            await @event.SendEmail(guest);

            _context.Entry(@event.Template).State = EntityState.Unchanged;

            _context.Events.AddOrUpdate(@event);
            _context.Guests.AddOrUpdate(guest);
            _context.SaveChanges();

            model = Mapper.Map<GuestViewModel>(guest);
            return Ok(model);
        }

        [HttpPost, Route("{id:int}/updateregistration")]
        public async Task<object> UpdateRegistration(int id, [FromBody]GuestViewModel model)
        {
            var @event = await _context.Events.SingleOrDefaultAsync(e => e.Id == id);
            if (@event == null) return BadRequest("Event not found");

            var guest = await _context.Guests.FirstOrDefaultAsync(e => e.Id == model.Id);
            if (guest == null) return BadRequest("Guest not found");

            var dto = Mapper.Map<Guest>(model);
            if (guest != dto) await CreateDemographicRecord(guest);

            Mapper.Map(model, guest);
            guest.ResponseDate = DateTime.Now;

            @event.AddTickets(guest, model.AdditionalTickets);

            _context.Events.AddOrUpdate(@event);
            _context.Guests.AddOrUpdate(guest);
            _context.SaveChanges();

            model = Mapper.Map<GuestViewModel>(guest);
            return Ok(model);
        }

        [HttpPost, Route("{id:int}/removewaiting")]
        public async Task<object> RemoveFromWaiting(int id, [FromBody]GuestViewModel model)
        {
            var @event = await _context.Events.SingleOrDefaultAsync(e => e.Id == id);
            if (@event == null) return BadRequest("Event not found");

            var guest = await _context.Guests.FirstOrDefaultAsync(e => e.Id == model.Id);
            if (guest == null) return BadRequest("Guest not found");

            var dto = Mapper.Map<Guest>(model);
            Mapper.Map(model, guest);

            @event.RemoveFromWaiting(guest);
            _context.Entry(@event.Template).State = EntityState.Unchanged;

            _context.Events.AddOrUpdate(@event);
            _context.Guests.AddOrUpdate(guest);
            _context.SaveChanges();

            model = Mapper.Map<GuestViewModel>(guest);
            return Ok(model);
        }

        [HttpPost, Route("{id:int}/CancelRegister/{guestId:int}")]
        public async Task<object> CancelRegistration(int id, int guestId)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (@event == null) return BadRequest("Event not found");

            var guest = await _context.Guests.FirstOrDefaultAsync(e => e.Id == guestId);
            if (guest == null) return BadRequest("No guest found");

            @event.CancelRegistration(guest);

            //_context.Entry(@event.Template).State = EntityState.Unchanged;

            _context.Events.AddOrUpdate(@event);

            _context.Guests.AddOrUpdate(guest);

            _context.SaveChanges();
            var dto = Mapper.Map<GuestViewModel>(guest);
            return Ok(dto);
        }

        [HttpPost, Route("{id:int}/mailticket/{guestId:int}")]
        public async Task<object> MailTicket(int id, int guestId)
        {
            var @event = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (@event == null) return BadRequest("Event not found");

            var guest = await _context.Guests.FirstOrDefaultAsync(e => e.Id == guestId);

            if (guest == null) return BadRequest("Guest not found");

            @event.MailTicket(guest);

            _context.Events.AddOrUpdate(@event);
            _context.Guests.AddOrUpdate(guest);
            _context.SaveChanges();

            var dto = Mapper.Map<GuestViewModel>(guest);
            return Ok(dto);
        }

        [HttpGet, Route("guest/{id:int}")]
        public async Task<object> GetGuest(int id)
        {
            var guest = await _context.Guests.Include("Event").ProjectTo<GuestViewModel>().FirstOrDefaultAsync(e => e.Id == id);
            if (guest == null) return BadRequest("Guest not found");
            return Ok(guest);
        }

        [HttpPost, Route("{id:int}/sendalltickets")]
        public async Task<object> SendAllTickets(int id)
        {
            try
            {
                var @event = await _context.Events.SingleOrDefaultAsync(e => e.Id == id);
                if (@event == null) return BadRequest("Event not found");

                _context.Guests
                    .Where(x => x.EventId == id && x.IsMailed == false && x.IsAttending == true && x.IsWaiting == false)
                    .Update(t => new Guest() { IsMailed = true, MailedDate = DateTime.Now });
                _context.SaveChanges();

                @event.TicketMailedCount = _context.Guests.Where(g => g.EventId == id && g.IsAttending == true && g.IsWaiting == false).Sum(g => g.TicketCount) ?? 0;
                _context.SaveChanges();

                return Ok(@event);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost, Route("{id:int}/sendallwaiting")]
        public async Task<object> SendAllWaiting(int id)
        {
            try
            {
                var @event = await _context.Events.SingleOrDefaultAsync(e => e.Id == id);
                if (@event == null) return NotFound();

                _context.Guests
                    .Where(x => x.EventId == id && x.IsMailed == false && x.IsAttending == true && x.IsWaiting == true)
                    .Update(t => new Guest() { IsMailed = true, MailedDate = DateTime.Now });
                _context.SaveChanges();

                _context.Events.AddOrUpdate(@event);
                _context.SaveChanges();

                return Ok(@event);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet, Route("{id:int}/guests/export")]
        public async Task<object> Export(int id, [FromUri]GuestSearchModel pager)
        {
            try
            {
                if (pager == null) pager = new GuestSearchModel();

                var ticketMailed = pager.IsMailed ?? false;
                var isWaiting = pager.IsWaiting ?? false;
                var isAttending = pager.IsAttending ?? false;
                var query = _context.Guests.Where(e => e.EventId == id);

                var pred = PredicateBuilder.True<Guest>();
                if (!string.IsNullOrWhiteSpace(pager.Address)) pred = pred.And(p => p.Address.Contains(pager.Address));
                if (!string.IsNullOrWhiteSpace(pager.FinderNumber)) pred = pred.And(p => p.FinderNumber.StartsWith(pager.FinderNumber));
                if (!string.IsNullOrWhiteSpace(pager.Name)) pred = pred.And(p => p.Name.Contains(pager.Name));
                if (!string.IsNullOrWhiteSpace(pager.City)) pred = pred.And(p => p.City.StartsWith(pager.City));
                if (!string.IsNullOrWhiteSpace(pager.State)) pred = pred.And(p => p.State.Equals(pager.State));
                if (!string.IsNullOrWhiteSpace(pager.ZipCode)) pred = pred.And(p => p.Zipcode.StartsWith(pager.ZipCode));
                if (!string.IsNullOrWhiteSpace(pager.Phone)) pred = pred.And(p => p.Phone.Contains(pager.Phone));
                if (!string.IsNullOrWhiteSpace(pager.Email)) pred = pred.And(p => p.Email.StartsWith(pager.Email));
                if (!string.IsNullOrWhiteSpace(pager.LookupId)) pred = pred.And(p => p.LookupId.StartsWith(pager.LookupId));
                if (!string.IsNullOrWhiteSpace(pager.ConstituentType)) pred = pred.And(p => p.ConstituentType.StartsWith(pager.ConstituentType));
                if (pager.IsMailed != null) pred = pred.And(p => p.IsMailed == ticketMailed);
                if (pager.IsWaiting != null) pred = pred.And(p => p.IsWaiting == isWaiting);
                if (pager.IsAttending != null) pred = pred.And(p => p.IsAttending == isAttending);

                var filteredQuery = query.Where(pred);

                var results = await filteredQuery
                    .ProjectTo<GuestExportViewModel>().ToListAsync();

                var path = HttpContext.Current.Server.MapPath(@"~\app_data\guestlist.csv");

                using (var csv = new CsvWriter(new StreamWriter(File.Create(path))))
                {
                    csv.Configuration.RegisterClassMap<GuestExportMap>();
                    csv.WriteHeader<GuestExportViewModel>();
                    csv.WriteRecords(results);
                }
                var filename = $"guest-list-{DateTime.Now:u}.csv";

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                response.Content = new StreamContent(stream);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = filename
                };
                response.Content.Headers.Add("x-filename", filename);

                return ResponseMessage(response);

            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpGet, Route("eventnameurlavailable/{name}")]
        public async Task<bool> EventNameUrlAvailable(string name)
        {
            return await _context.Events.Where(
                    e => e.NameUrl == name && e.EndDate >= DateTime.Now).CountAsync() == 0;

        }

        private async Task CreateDemographicRecord(Guest dto)
        {
            var demo = _context.DemographicChanges.FirstOrDefault(d => d.FinderNumber == dto.FinderNumber);

            if (demo == null)
            {
                demo = new DemographicChange()
                {
                    LookupId = dto.LookupId,
                    FinderNumber = dto.FinderNumber,
                    Name = dto.Name,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Street = dto.Address,
                    Street2 = dto.Address2,
                    City = dto.City,
                    State = dto.State,
                    Zipcode = dto.Zipcode,
                    Source = Source.RSVP
                };
            }
            else
            {
                demo.Name = dto.Name;
                demo.Email = dto.Name;
                demo.Phone = dto.Name;
                demo.Street = dto.Address;
                demo.Street2 = dto.Address2;
                demo.City = dto.City;
                demo.State = dto.State;
                demo.Zipcode = dto.Zipcode;
            }
            _context.DemographicChanges.AddOrUpdate(demo);
            await _context.SaveChangesAsync();
        }
    }
}