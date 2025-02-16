﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniqueStandardProject.Areas.Products.Models;
using UniqueStandardProject.Areas.UserManage.Models;
using UniqueStandardProject.Data;
using UniqueStandardProject.Entities;
using UniqueStandardProject.Interfaces;

namespace UniqueStandardProject.Areas.Products.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly UniqueStandardDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IFileService _imageService;

        public ProductsController(UniqueStandardDbContext context, IWebHostEnvironment hostEnvironment, IFileService imageService)
        {
            _context = context;
            _hostingEnvironment = hostEnvironment;
            _imageService = imageService;
        }

        [BindProperty]
        public IFormFile ImageFile { get; set; }

        [BindProperty]
        public string Base64String_Photo { get; set; }

        #region Products
        [HttpGet]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductList()
        {
            var list = await _context.Products.OrderBy(p => p.SortOrder).ToListAsync();

            return Ok(new ResponseModel()
            {
                Meta = new { total = list.Count },
                Data = list
            });
        }
        #endregion

        #region ProductDetail
        [HttpGet("productDetail")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductDetail(int? productId)
        {
            IQueryable<ProductModel> query = from productdetail in _context.ProductDetails
                                             join products in _context.Products
                                             on productdetail.ProductId equals products.ProductId
                                             select new ProductModel()
                                             {
                                                 DetailId = productdetail.DetailId,
                                                 ProductId = products.ProductId,
                                                 Product = products.Product1,
                                                 SortOrder = productdetail.SortOrder,
                                                 Img = productdetail.Img,
                                                 Title = productdetail.Title,
                                                 Description = productdetail.Description
                                             };

            List<ProductModel> list = (productId.HasValue) ? await query.Where(q => q.ProductId == productId.Value).OrderBy(p => p.SortOrder).ToListAsync() : await query.ToListAsync();

            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = list,
                Meta = new { total_count = list.Count },
            });
        }

        [HttpPost("productDetail/delete")]
        public async Task<IActionResult> DeleteProduct(int detailId, int productId)
        {
            var productDetail = await _context.ProductDetails.Where(p => p.DetailId == detailId && p.ProductId == productId).FirstOrDefaultAsync();

            if (productDetail != null)
            {
                _context.ProductDetails.Remove(productDetail);
                await _context.SaveChangesAsync();
                return Ok(new ResponseModel()
                {
                    Message = "Successfully Deleted!",
                    Code = StatusCodes.Status200OK,
                    Success = true
                });
            }
            else
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "Unsuccessfully!"
                });
            }
        }

        [HttpPost("productDetail/edit")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> EditProductDetail([FromForm] EditProductModel model)
        {
            Entities.ProductDetail productDetail = await _context.ProductDetails.Where(p => p.DetailId == model.DetailId && p.ProductId == model.ProductId).FirstOrDefaultAsync();
            if (productDetail != null)
            {
                productDetail.SortOrder = model.SortOrder;
                productDetail.Title = model.Title;
                productDetail.Description = model.Desc;
            }

            ImageFile = model.Image;

            if (ImageFile != null)
            {
                using var stream = new MemoryStream();
                ImageFile.CopyTo(stream);
                var bytes = stream.ToArray();
                string image = Convert.ToBase64String(bytes);
                Image image1 = Image.FromStream(stream);
                string extension = ("." + ImageFile.FileName.Split('.')[^1]).ToLower();
                productDetail.Img = $"images/productDetail/{productDetail.DetailId}-{productDetail.ProductId}{extension}";
                _imageService.WriteImage(image1, productDetail.DetailId.ToString() + "-" + productDetail.ProductId.ToString(), $"{productDetail.DetailId}-{productDetail.ProductId}.jpg", "productDetail");
            }

            _context.Entry(productDetail).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return Ok(new ResponseModel()
            {
                Data = productDetail,
                Success = true,
                Code = StatusCodes.Status200OK
            });
        }

        [HttpGet("productDetail/Except")]
        public async Task<IActionResult> GetExceptProductDetail(int productId, int detailId)
        {
            List<ProductDetail> productDetails = await _context.ProductDetails.Where(p => p.ProductId == productId && p.DetailId != detailId).ToListAsync();
            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = productDetails.Count },
                Data = productDetails
            });
        }

        [HttpGet("productDetail/relatedProduct")]
        public async Task<IActionResult> GetRelatedProductList(int? detailId)
        {
            if(detailId is null)
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "DetailId is required."
                });
            }

            var productDetail = await _context.ProductDetails
                                      .FirstOrDefaultAsync(p => p.DetailId == detailId);

            if (productDetail == null || string.IsNullOrEmpty(productDetail.RelatedId))
            {
                return NotFound(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status404NotFound,
                    Message = "Product detail not found or relatedId is empty."
                });
            }

            var relatedIds = productDetail.RelatedId.Split(',')
                                                .Select(int.Parse).ToList();



            var relatedProducts = await _context.ProductDetails
                                        .Where(p => relatedIds.Contains(p.DetailId))
                                        .ToListAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = relatedProducts.Count },
                Data = relatedProducts
            });

        }

        #endregion

        #region ProductImg
        [HttpGet("productImg")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductImgList()
        {
            IQueryable<ProductModel> query = from productdetail in _context.ProductDetails
                                             join products in _context.Products
                                             on productdetail.ProductId equals products.ProductId
                                             select new ProductModel()
                                             {
                                                 ProductId = products.ProductId,
                                                 DetailId = productdetail.DetailId,
                                                 Product = products.Product1,
                                                 Img = productdetail.Img,
                                                 Title = productdetail.Title,
                                             };

            List<ProductModel> list = await query.OrderBy(s => s.SortOrder).ToListAsync();

            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = list,
                Meta = new { total_count = list.Count },
            });
        }

        [HttpGet("productImgList")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllProductImgList()
        {
            IQueryable<ProductModel> query = from productdetail in _context.ProductDetails
                                             join products in _context.Products
                                             on productdetail.ProductId equals products.ProductId
                                             join productImg in _context.ProductImgs
                                             on productdetail.DetailId equals productImg.DetailId
                                             select new ProductModel()
                                             {
                                                 ImageId = productImg.ImageId,
                                                 Product = products.Product1,
                                                 Img = productImg.Img,
                                                 Title = productdetail.Title,
                                             };

            List<ProductModel> list = await query.ToListAsync();

            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = list,
                Meta = new { total_count = list.Count },
            });
        }

        [HttpGet("productTitle")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetProductTitle()
        {
            var list = await _context.ProductDetails.ToListAsync();
            return Ok(new ResponseModel()
            {
                Meta = new { total = list.Count },
                Data = list
            });
        }

        [HttpGet("ImgTitle")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetImgTitleChange(int? detailId)
        {
            IQueryable<ProductModel> query = from productdetail in _context.ProductDetails
                                             join productImg in _context.ProductImgs
                                             on productdetail.DetailId equals productImg.DetailId
                                             join products in _context.Products
                                             on productdetail.ProductId equals products.ProductId
                                             select new ProductModel()
                                             {
                                                 ImageId = productImg.ImageId,
                                                 Product = products.Product1,
                                                 DetailId = productdetail.DetailId,
                                                 Img = productImg.Img,
                                                 Title = productImg.Title,
                                             };

            List<ProductModel> list = (detailId.HasValue) ? await query.Where(q => q.DetailId == detailId.Value).ToListAsync() : await query.ToListAsync();

            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = list,
                Meta = new { total_count = list.Count },
            });
        }

        [HttpPost("deleteImg")]
        public async Task<IActionResult> DeleteImages(int imageId)
        {
            Entities.ProductImg imageToDelete = _context.ProductImgs.FirstOrDefault(i => i.ImageId == imageId);

            if (imageToDelete != null)
            {

                // Delete the image file from the server (if applicable)
                string serverPathToDelete = Path.Combine(_hostingEnvironment.WebRootPath, imageToDelete.Img);

                if (System.IO.File.Exists(serverPathToDelete))
                {
                    System.IO.File.Delete(serverPathToDelete);
                }

                // Delete the image from your data store (e.g., database)
                _context.ProductImgs.Remove(imageToDelete);
                await _context.SaveChangesAsync();
            }
                
            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = null,
            });
        }
        #endregion

        #region Master
        [HttpGet("masterList")]
        public async Task<IActionResult> GetMasterList()
        {
            var list = await _context.MasterTbls.ToListAsync();
            return Ok(new ResponseModel()
            {
                Code = StatusCodes.Status200OK,
                Success = true,
                Data = list,
                Meta = new { total_count = list.Count },
            });
        }
        #endregion

        #region Distributor

        [HttpGet("distributorList")]
        public async Task<IActionResult> GetdistributorList()
        {
            var list = await _context.Distributors.OrderBy(d => d.SortOrder).ToListAsync();
            return Ok(new ResponseModel()
            {
                Meta = new { total = list.Count },
                Data = list
            });
        }

        [HttpPost("distributor/delete")]
        public async Task<IActionResult> DeleteDistributor(int id)
        {
            var distributor = await _context.Distributors.Where(d => d.Id == id).FirstOrDefaultAsync();

            if (distributor != null)
            {
                _context.Distributors.Remove(distributor);
                await _context.SaveChangesAsync();
                return Ok(new ResponseModel()
                {
                    Message = "Successfully Deleted!",
                    Code = StatusCodes.Status200OK,
                    Success = true
                });
            }
            else
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "Unsuccessfully!"
                });
            }
        }

        #endregion

        #region Service

        [HttpGet("serviceList")]
        public async Task<IActionResult> GetServiceList()
        {
            var list = await _context.ServiceTbls.OrderBy(s => s.SortOrder).ToListAsync();
            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = list.Count },
                Data = list
            });
        }

        [HttpGet("serviceTitle")]
        public async Task<IActionResult> GetServiceTitle()
        {
            var serviceTitles = await _context
                                        .ServiceTbls
                                        .Select(s => new {s.ServiceId, s.Title})
                                        .Distinct()
                                        .ToListAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = serviceTitles.Count },
                Data = serviceTitles
            });
        }

        [HttpGet("getImage")]
        public async Task<IActionResult> GetImage(string title)
        {
            var imageList = await _context.ServiceTbls.Where(s => s.Title == title).ToListAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = imageList.Count },
                Data = imageList
            });
        }

        [HttpPost("service/delete")]
        public async Task<IActionResult> DeleteService(int serviceId)
        {
            var service = await _context.ServiceTbls.Where(d => d.ServiceId == serviceId).FirstOrDefaultAsync();

            if (service != null)
            {
                _context.ServiceTbls.Remove(service);
                await _context.SaveChangesAsync();
                return Ok(new ResponseModel()
                {
                    Message = "Successfully Deleted!",
                    Code = StatusCodes.Status200OK,
                    Success = true
                });
            }
            else
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "Unsuccessfully!"
                });
            }
        }

        [HttpPost("service/edit")]
        [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> EditService([FromForm] EditProductModel model)
        {
            Entities.ServiceTbl service = await _context.ServiceTbls.Where(p => p.ServiceId == model.ServiceId).FirstOrDefaultAsync();
            if (service != null)
            {
                service.SortOrder = model.SortOrder;
                service.Title = model.Title;
                service.Desctiption = model.Desc;
            }

            ImageFile = model.Image;

            if (ImageFile != null)
            {
                using var stream = new MemoryStream();
                ImageFile.CopyTo(stream);
                var bytes = stream.ToArray();
                string image = Convert.ToBase64String(bytes);
                Image image1 = Image.FromStream(stream);
                string extension = ("." + ImageFile.FileName.Split('.')[^1]).ToLower();
                service.Img = $"images/service/{service.ServiceId}{extension}";
                _imageService.WriteImage(image1, service.ServiceId.ToString(), $"{service.ServiceId}.jpg", "service");
            }

            _context.Attach(service).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return Ok(new ResponseModel()
            {
                Data = service,
                Success = true,
                Code = StatusCodes.Status200OK
            });
        }

        #endregion

        #region RelatedItem
        [HttpPost("relatedProduct")]
        public async Task<IActionResult> GetRelated(int detailId, int detailId1)
        {
            var productDetail = await _context.ProductDetails.FirstOrDefaultAsync(p => p.DetailId == detailId);

            List<string> relatedItem = new List<string>();

            if (productDetail.RelatedId == null)
            {
                productDetail.RelatedId = detailId1.ToString();
            }
            else
            {
                relatedItem = productDetail.RelatedId.Split(",").ToList();

                if (relatedItem.Contains(detailId1.ToString()))
                {
                    return BadRequest(new ResponseModel()
                    {
                        Success = true,
                        Code = StatusCodes.Status400BadRequest,
                        Meta = null,
                        Data = null
                    });
                }
                productDetail.RelatedId = productDetail.RelatedId + "," + detailId1.ToString();
            }

            _context.Attach(productDetail).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = null,
                Data = productDetail
            });
        }


        [HttpGet("Related")]
        public async Task<IActionResult> GetRelatedProduct(int detailId)
        {
            var query = from productDetails in _context.ProductDetails
                        join products in _context.Products
                        on productDetails.ProductId equals products.ProductId
                        select new ProductModel()
                        {
                            DetailId = productDetails.DetailId,
                            ProductId = productDetails.ProductId,
                            Product = products.Product1,
                            Img = productDetails.Img,
                            Title = productDetails.Title,
                            Description = productDetails.Description
                        };


            var productDetail = await query.FirstOrDefaultAsync(p => p.DetailId == detailId);

            return Ok(new ResponseModel()
            {
                Data = productDetail,
                Success = true,
                Code = StatusCodes.Status200OK
            });
        }

        #endregion

        #region Activity
        [HttpGet("activity")]
        public async Task<IActionResult> GetActivity()
        {
            var activityList = await _context.Activities.OrderBy(a => a.SortOrder).ToListAsync();
            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = activityList.Count },
                Data = activityList
            });
        }

        [HttpPost("activity/edit")]
        public async Task<IActionResult> EditActivity([FromForm] EditActivityModel model)
        {
            Entities.Activity activity = await _context.Activities.FirstOrDefaultAsync(a => a.ActivityId == model.ActivityId);
            if (activity != null)
            {
                activity.Title = model.Title;
                activity.SortOrder = model.SortOrder;
                activity.UpdateDate = DateTime.Now;
                activity.UpdateUser = User.Identity.Name;
            }

            ImageFile = model.Image;

            if (ImageFile != null)
            {
                using var stream = new MemoryStream();
                ImageFile.CopyTo(stream);
                var bytes = stream.ToArray();
                string image = Convert.ToBase64String(bytes);
                Image image1 = Image.FromStream(stream);
                string extension = ("." + ImageFile.FileName.Split('.')[^1]).ToLower();
                activity.Img = $"images/activity/{activity.ActivityId}{extension}";
                _imageService.WriteImage(image1, activity.ActivityId.ToString(), $"{activity.ActivityId}.jpg", "activity");
            }

            _context.Attach(activity).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return Ok(new ResponseModel()
            {
                Data = activity,
                Success = true,
                Code = StatusCodes.Status200OK
            });

        }

        [HttpPost("activity/delete")]
        public async Task<IActionResult> DeleteActivity(int activityId)
        {
            var activity = await _context.Activities.FindAsync(activityId);

            if (activity != null)
            {
                _context.Activities.Remove(activity);
                await _context.SaveChangesAsync();
                return Ok(new ResponseModel()
                {
                    Message = "Successfully Deleted!",
                    Success = true,
                    Code = StatusCodes.Status200OK
                });
            }
            else
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "Unsuccessfully!"
                });
            }
        }
        #endregion

        #region WeldingTraining
        [HttpGet("weldingtraining")]
        public async Task<IActionResult> GetWelding()
        {
            var weldingList = await _context.WeldingTrainings.OrderBy(w => w.SortOrder).ToListAsync();
            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = weldingList.Count },
                Data = weldingList
            });
        }

        [HttpGet("weldingTitle")]
        public async Task<IActionResult> GetWeldingTitle()
        {
            var distinctTitles = await _context
                                        .WeldingTrainings
                                        .Select(s => new { s.WeldingId, s.Title })
                                        .Distinct()
                                        .ToListAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = distinctTitles.Count },
                Data = distinctTitles
            });
        }

        [HttpPost("weldingtraining/edit")]
        public async Task<IActionResult> EditWeldingTraining([FromForm] EditWeldingTrainingModel model)
        {
            Entities.WeldingTraining welding = await _context.WeldingTrainings.FirstOrDefaultAsync(a => a.WeldingId == model.WeldingId);
            if (welding != null)
            {
                welding.Title = model.Title;
                welding.SortOrder = model.SortOrder;
                welding.UpdateDate = DateTime.Now;
                welding.UpdateUser = User.Identity.Name;
            }

            ImageFile = model.Image;

            if (ImageFile != null)
            {
                using var stream = new MemoryStream();
                ImageFile.CopyTo(stream);
                var bytes = stream.ToArray();
                string image = Convert.ToBase64String(bytes);
                Image image1 = Image.FromStream(stream);
                string extension = ("." + ImageFile.FileName.Split('.')[^1]).ToLower();
                welding.Img = $"images/welding/{welding.WeldingId}{extension}";
                _imageService.WriteImage(image1, welding.WeldingId.ToString(), $"{welding.WeldingId}.jpg", "welding");
            }

            _context.Attach(welding).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return Ok(new ResponseModel()
            {
                Data = welding,
                Success = true,
                Code = StatusCodes.Status200OK
            });
        }

        [HttpPost("weldingtraining/delete")]
        public async Task<IActionResult> DeleteWeldingTraining(int weldingId)
        {
            var welding = await _context.WeldingTrainings.FindAsync(weldingId);

            if (welding != null)
            {
                _context.WeldingTrainings.Remove(welding);
                await _context.SaveChangesAsync();
                return Ok(new ResponseModel()
                {
                    Message = "Successfully Deleted!",
                    Success = true,
                    Code = StatusCodes.Status200OK
                });
            }
            else
            {
                return BadRequest(new ResponseModel()
                {
                    Success = false,
                    Code = StatusCodes.Status400BadRequest,
                    Message = "Unsuccessfully!"
                });
            }
        }

        [HttpGet("trainingImage")]
        public async Task<IActionResult> GetTrainingImage(string title)
        {
            var imageList = await _context.WeldingTrainings.Where(s => s.Title == title).ToListAsync();

            return Ok(new ResponseModel()
            {
                Success = true,
                Code = StatusCodes.Status200OK,
                Meta = new { total_count = imageList.Count },
                Data = imageList
            });
        }
        #endregion
    }
}
