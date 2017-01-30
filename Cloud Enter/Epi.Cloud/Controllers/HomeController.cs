﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Security;
using Epi.Cloud.Common.Constants;
using Epi.Cloud.Common.Metadata;
using Epi.Cloud.Facades.Interfaces;
using Epi.Cloud.Interfaces.MetadataInterfaces;
using Epi.Cloud.MVC.Extensions;
using Epi.Core.EnterInterpreter;
using Epi.DataPersistence.Constants;
using Epi.FormMetadata.DataStructures;
using Epi.Cloud.Common.DTO;
using Epi.Cloud.Common.Extensions;
using Epi.Cloud.Common.Message;
using Epi.Cloud.Common.Model;
using Epi.Cloud.Interfaces.DataInterfaces;
using Epi.Web.MVC.Models;
using Epi.Web.MVC.Utility;
using Epi.Common.EmailServices;

namespace Epi.Web.MVC.Controllers
{
    [Authorize]
    public class HomeController : BaseSurveyController
    {
        private readonly ISecurityFacade _securityFacade;
        private readonly Epi.Cloud.CacheServices.IEpiCloudCache _cacheServices;

        private readonly ISurveyResponseDao _surveyResponseDao;

        private IEnumerable<AbridgedFieldInfo> _pageFields;
        private string _requiredList = "";

        /// <summary>
        /// injecting surveyFacade to the constructor 
        /// </summary>
        /// <param name="surveyFacade"></param>
        public HomeController(ISurveyFacade surveyFacade,
                              ISecurityFacade securityFacade,
                              Epi.Cloud.Interfaces.MetadataInterfaces.IProjectMetadataProvider projectMetadataProvider,
                              Epi.Cloud.CacheServices.IEpiCloudCache iCacheServices,
                              ISurveyResponseDao surveyResponseDao)
        {
            _surveyFacade = surveyFacade;
            _securityFacade = securityFacade;
            _projectMetadataProvider = projectMetadataProvider;
            _cacheServices = iCacheServices;
            _surveyResponseDao = surveyResponseDao;
        }

        public ActionResult Default()
        {
            return View("Default");
        }

        [HttpGet]
        public ActionResult Index(string surveyId, int orgId = -1)
        {

            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            int orgnizationId;
            Session[SessionKeys.EditForm] = null;

            Guid userIdGuid = new Guid();
            try
            {

                FormModel formModel = GetFormModel(surveyId, userId, userIdGuid, out orgnizationId);

                if (orgId == -1)
                {
                    Session[SessionKeys.SelectedOrgId] = orgnizationId;
                }
                else
                {
                    Session[SessionKeys.SelectedOrgId] = orgId;
                }
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"(\r\n|\r|\n)+");



                bool IsMobileDevice = false;
                IsMobileDevice = this.Request.Browser.IsMobileDevice;
                if (IsMobileDevice) // Because mobile doesn't need RootFormId until button click. 
                {
                    Session[SessionKeys.RootFormId] = null;
                    Session[SessionKeys.PageNumber] = null;
                    Session[SessionKeys.SortOrder] = null;
                    Session[SessionKeys.SortField] = null;
                    Session[SessionKeys.SearchCriteria] = null;
                    Session[SessionKeys.SearchModel] = null;
                }

                string surveyMode = "";
                Omniture OmnitureObj = Epi.Web.MVC.Utility.OmnitureHelper.GetSettings(surveyMode, IsMobileDevice);
                ViewBag.Omniture = OmnitureObj;

                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                ViewBag.Version = version;

                return View(Epi.Cloud.Common.Constants.Constant.INDEX_PAGE, formModel);
            }
            catch (Exception ex)
            {
                Epi.Web.Utility.ExceptionMessage.SendLogMessage(ex, this.HttpContext);
                ExceptionModel ExModel = new ExceptionModel();
                ExModel.ExceptionDetail = ex.StackTrace;
                ExModel.Message = ex.Message;
                return View(Epi.Cloud.Common.Constants.Constant.EXCEPTION_PAGE, ExModel);
            }
        }

        private FormModel GetFormModel(string surveyId, int userId, Guid userIdGuid, out int orgnizationId)
        {
            FormModel formModel = new Models.FormModel();
            formModel.UserHighestRole = int.Parse(Session[SessionKeys.UserHighestRole].ToString());
            // Get OrganizationList
            OrganizationRequest request = new OrganizationRequest();
            request.UserId = userId;
            request.UserRole = formModel.UserHighestRole;
            OrganizationResponse organizations = _securityFacade.GetOrganizationsByUserId(request);

            formModel.OrganizationList = organizations.OrganizationList.ToOrganizationModelList();
            //Get Forms
            orgnizationId = organizations.OrganizationList[0].OrganizationId;
            formModel.FormList = GetFormsInfoList(userIdGuid, orgnizationId);
            // Set user Info

            formModel.UserFirstName = Session[SessionKeys.UserFirstName].ToString();
            formModel.UserLastName = Session[SessionKeys.UserLastName].ToString();
            formModel.SelectedForm = surveyId;
            return formModel;
        }

        /// <summary>
        /// redirecting to Survey controller to action method Index
        /// </summary>
        /// <param name="surveyModel"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Index(string surveyId, string addNewFormId, string editForm)
        {
            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            string UserName = Session[SessionKeys.UserName].ToString();
            Session[SessionKeys.FormValuesHasChanged] = "";

            if (string.IsNullOrEmpty(editForm) && Session[SessionKeys.EditForm] != null)
            {
                editForm = Session[SessionKeys.EditForm].ToString();
            }

            if (!string.IsNullOrEmpty(editForm) && string.IsNullOrEmpty(addNewFormId))
            {
                Session[SessionKeys.RootResponseId] = editForm;

                Session[SessionKeys.IsEditMode] = true;
                SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(editForm, Session[SessionKeys.RootFormId].ToString());


                Session[SessionKeys.RequestedViewId] = surveyAnswerDTO.ViewId;
                if (Session[SessionKeys.RecoverLastRecordVersion] != null)
                {
                    surveyAnswerDTO.RecoverLastRecordVersion = bool.Parse(Session[SessionKeys.RecoverLastRecordVersion].ToString());
                }
                string childRecordId = GetChildRecordId(surveyAnswerDTO);
                Session[SessionKeys.RecoverLastRecordVersion] = false;
                return RedirectToAction(Epi.Cloud.Common.Constants.Constant.INDEX, Epi.Cloud.Common.Constants.Constant.SURVEY_CONTROLLER, new { responseid = childRecordId, PageNumber = 1, surveyid = surveyAnswerDTO.SurveyId, Edit = "Edit" });
            }
            else
            {
                Session[SessionKeys.IsEditMode] = false;
            }
            bool isMobileDevice = this.Request.Browser.IsMobileDevice;


            if (isMobileDevice == false)
            {
                isMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
            }

            FormsAuthentication.SetAuthCookie("BeginSurvey", false);

            //create the responseid
            Guid responseId = Guid.NewGuid();
            TempData[Epi.Cloud.Common.Constants.Constant.RESPONSE_ID] = responseId.ToString();

            // create the first survey response
            // Epi.Cloud.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.CreateSurveyAnswer(surveyModel.SurveyId, ResponseID.ToString());
            Session[SessionKeys.RootFormId] = addNewFormId;
            Session[SessionKeys.RootResponseId] = responseId;

            int currentOrgId = int.Parse(Session[SessionKeys.SelectedOrgId].ToString());

            SurveyAnswerDTO surveyAnswer = _surveyFacade.CreateSurveyAnswer(addNewFormId, responseId.ToString(), userId, false, "", false, currentOrgId);
            surveyId = surveyId ?? surveyAnswer.SurveyId;

            // Initialize the Metadata Accessor
            MetadataAccessor.CurrentFormId = surveyId;

            MvcDynamicForms.Form form = _surveyFacade.GetSurveyFormData(surveyAnswer.SurveyId, 1, surveyAnswer, isMobileDevice);
            SurveyInfoModel surveyInfoModel = form.SurveyInfo.ToFormInfoModel();

            MetadataAccessor metadataAccessor = form.SurveyInfo as MetadataAccessor;

            // set the survey answer to be production or test 
            surveyAnswer.IsDraftMode = form.SurveyInfo.IsDraftMode;

            TempData["Width"] = form.Width + 100;

            string checkcode = metadataAccessor.GetFormDigest(surveyId).CheckCode;
            form.FormCheckCodeObj = form.GetCheckCodeObj(metadataAccessor.GetFieldDigests(surveyId), surveyAnswer.ResponseDetail, checkcode);

            ///////////////////////////// Execute - Record Before - start//////////////////////
            Dictionary<string, string> contextDetailList = new Dictionary<string, string>();
            EnterRule functionObject_B = (EnterRule)form.FormCheckCodeObj.GetCommand("level=record&event=before&identifier=");
            SurveyResponseHelper surveyResponseHelper = new SurveyResponseHelper(_pageFields, _requiredList);
            if (functionObject_B != null && !functionObject_B.IsNull())
            {
                try
                {
                    PageDigest[] pageDigests = form.MetadataAccessor.GetCurrentFormPageDigests();
                    var responseDetail = surveyAnswer.ResponseDetail;

                    responseDetail = surveyResponseHelper.CreateResponseDocument(pageDigests);
                    Session[SessionKeys.RequiredList] = surveyResponseHelper.RequiredList;
                    this._requiredList = surveyResponseHelper.RequiredList;
                    form.RequiredFieldsList = this._requiredList;
                    functionObject_B.Context.HiddenFieldList = form.HiddenFieldsList;
                    functionObject_B.Context.HighlightedFieldList = form.HighlightedFieldsList;
                    functionObject_B.Context.DisabledFieldList = form.DisabledFieldsList;
                    functionObject_B.Context.RequiredFieldList = form.RequiredFieldsList;

                    functionObject_B.Execute();

                    // field list
                    form.HiddenFieldsList = functionObject_B.Context.HiddenFieldList;
                    form.HighlightedFieldsList = functionObject_B.Context.HighlightedFieldList;
                    form.DisabledFieldsList = functionObject_B.Context.DisabledFieldList;
                    form.RequiredFieldsList = functionObject_B.Context.RequiredFieldList;


                    contextDetailList = Epi.Web.MVC.Utility.SurveyHelper.GetContextDetailList(functionObject_B);
                    form = Epi.Web.MVC.Utility.SurveyHelper.UpdateControlsValuesFromContext(form, contextDetailList);

                    _surveyFacade.UpdateSurveyResponse(surveyInfoModel,
                                                        responseId.ToString(),
                                                        form,
                                                        surveyAnswer,
                                                        false,
                                                        false,
                                                        0,
                                                        SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString()), UserName);
                }
                catch (Exception ex)
                {
                    // do nothing so that processing
                    // can continue
                }
            }
            else
            {
                PageDigest[] pageDigestArray = metadataAccessor.GetPageDigests(surveyInfoModel.SurveyId);

                surveyAnswer.ResponseDetail = surveyResponseHelper.CreateResponseDocument(pageDigestArray);

                this._requiredList = surveyResponseHelper.RequiredList;
                Session[SessionKeys.RequiredList] = surveyResponseHelper.RequiredList;
                form.RequiredFieldsList = _requiredList;
                //_surveyFacade.UpdateSurveyResponse(surveyInfoModel, surveyAnswer.ResponseId, form, surveyAnswer, false, false, 0, SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString()));
            }

            ///////////////////////////// Execute - Record Before - End//////////////////////
            return RedirectToAction(Epi.Cloud.Common.Constants.Constant.INDEX, Epi.Cloud.Common.Constants.Constant.SURVEY_CONTROLLER, new { responseid = responseId, PageNumber = 1, surveyid = surveyInfoModel.SurveyId });
        }

        private string GetChildRecordId(SurveyAnswerDTO surveyAnswerDTO)
        {
            SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
            SurveyAnswerResponse surveyAnswerResponse = new SurveyAnswerResponse();
            string childResponseId = Guid.NewGuid().ToString();
            surveyAnswerDTO.ParentRecordId = surveyAnswerDTO.ResponseId;
            surveyAnswerDTO.ResponseId = childResponseId;
            surveyAnswerDTO.Status = RecordStatus.InProcess;
            surveyAnswerDTO.ReasonForStatusChange = RecordStatusChangeReason.CreateMulti;
            surveyAnswerRequest.SurveyAnswerList.Add(surveyAnswerDTO);
            string result;

            //responseId = TempData[Epi.Cloud.Common.Constants.Constant.RESPONSE_ID].ToString();
            string userId = Session[SessionKeys.UserId].ToString();
            surveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(userId);
            surveyAnswerRequest.RequestId = childResponseId;
            surveyAnswerRequest.Action = "CreateMulti";
            surveyAnswerResponse = _surveyFacade.SetChildRecord(surveyAnswerRequest);
            result = surveyAnswerResponse.SurveyResponseList[0].ResponseId;
            return result;
        }

        [HttpGet]
        [Authorize]
        public ActionResult ReadResponseInfo(string formid, int page = 1)
        {
            bool isMobileDevice = this.Request.Browser.IsMobileDevice;

            var model = new FormResponseInfoModel();

            Session[SessionKeys.RootFormId] = formid;
            model = GetFormResponseInfoModel(formid, page);

            if (isMobileDevice == false)
            {
                return PartialView("ListResponses", model);
            }
            else
            {
                return View("ListResponses", model);
            }
        }

        [HttpGet]
        [Authorize]
        public ActionResult ReadSortedResponseInfo(string formId, int? page, string sort, string sortField, int orgId, bool reset = false)
        {
            Template projectMetadata = null;
            page = page.HasValue ? page.Value : 1;

            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

            //Code added to retain Search Starts

            if (reset)
            {
                Session[SessionKeys.SortOrder] = "";
                Session[SessionKeys.SortField] = "";

                // TODO: Temporary clear cache
                var sessionProjectId = Session[SessionKeys.ProjectId] as string;
                if (!string.IsNullOrWhiteSpace(sessionProjectId))
                {
                    _cacheServices.ClearAllCache(new Guid(sessionProjectId));
                }
            }

            Session[SessionKeys.SelectedOrgId] = orgId;
            if (Session[SessionKeys.RootFormId] != null && Session[SessionKeys.RootFormId].ToString() == formId)
            {
                if (Session[SessionKeys.SortOrder] != null &&
                    !string.IsNullOrEmpty(Session[SessionKeys.SortOrder].ToString()) &&
                    string.IsNullOrEmpty(sort))
                {
                    sort = Session[SessionKeys.SortOrder].ToString();
                }

                if (Session[SessionKeys.SortField] != null &&
                    !string.IsNullOrEmpty(Session[SessionKeys.SortField].ToString()) &&
                    string.IsNullOrEmpty(sortField))
                {
                    sortField = Session[SessionKeys.SortField].ToString();
                }

                Session[SessionKeys.SortOrder] = sort;
                Session[SessionKeys.SortField] = sortField;
                Session[SessionKeys.PageNumber] = page.Value;
            }
            else
            {
                Session.Remove("SortOrder");
                Session.Remove("SortField");
                Session[SessionKeys.RootFormId] = formId;
                Session[SessionKeys.PageNumber] = page.Value;

                if (Session[SessionKeys.ProjectId] == null)
                {
                    // Prime the cache
                    projectMetadata = _projectMetadataProvider.GetProjectMetadataAsync(ProjectScope.TemplateWithNoPages).Result;
                    Session[SessionKeys.ProjectId] = projectMetadata.Project.Id;
                }
            }
            //Code added to retain Search Ends. 

            var model = new FormResponseInfoModel();
            model = GetFormResponseInfoModel(formId, page.Value, sort, sortField, orgId);

            if (IsMobileDevice == false)
            {
                return PartialView("ListResponses", model);
            }
            else
            {
                return View("ListResponses", model);
            }
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ResetSort(string formid)
        {
            Session["SortOrder"] = null;
            Session["SortField"] = null;
            return Json(true);
        }
        private string CreateSearchCriteria(System.Collections.Specialized.NameValueCollection nameValueCollection, SearchBoxModel searchModel, FormResponseInfoModel model)
        {
            FormCollection formCollection = new FormCollection(nameValueCollection);

            StringBuilder searchBuilder = new StringBuilder();

            if (ValidateSearchFields(formCollection))
            {
                if (formCollection["col1"].Length > 0 && formCollection["val1"].Length > 0)
                {
                    searchBuilder.Append(formCollection["col1"] + "='" + formCollection["val1"] + "'");
                    searchModel.SearchCol1 = formCollection["col1"];
                    searchModel.Value1 = formCollection["val1"];
                }
                if (formCollection["col2"].Length > 0 && formCollection["val2"].Length > 0)
                {
                    searchBuilder.Append(" AND " + formCollection["col2"] + "='" + formCollection["val2"] + "'");
                    searchModel.SearchCol2 = formCollection["col2"];
                    searchModel.Value2 = formCollection["val2"];
                }
                if (formCollection["col3"].Length > 0 && formCollection["val3"].Length > 0)
                {
                    searchBuilder.Append(" AND " + formCollection["col3"] + "='" + formCollection["val3"] + "'");
                    searchModel.SearchCol3 = formCollection["col3"];
                    searchModel.Value3 = formCollection["val3"];
                }
                if (formCollection["col4"].Length > 0 && formCollection["val4"].Length > 0)
                {
                    searchBuilder.Append(" AND " + formCollection["col4"] + "='" + formCollection["val4"] + "'");
                    searchModel.SearchCol4 = formCollection["col4"];
                    searchModel.Value4 = formCollection["val4"];
                }
                if (formCollection["col5"].Length > 0 && formCollection["val5"].Length > 0)
                {
                    searchBuilder.Append(" AND " + formCollection["col5"] + "='" + formCollection["val5"] + "'");
                    searchModel.SearchCol5 = formCollection["col5"];
                    searchModel.Value5 = formCollection["val5"];
                }
            }

            return searchBuilder.ToString();
        }

        private bool ValidateSearchFields(FormCollection formCollection)
        {
            if (string.IsNullOrEmpty(formCollection["col1"]) || formCollection["col1"] == "undefined" ||
               string.IsNullOrEmpty(formCollection["val1"]) || formCollection["val1"] == "undefined")
            {
                return false;
            }
            return true;
        }

        private void PopulateDropDownlist(out List<SelectListItem> searchColumns, string selectedValue, List<KeyValuePair<int, string>> columns)
        {
            searchColumns = new List<SelectListItem>();
            foreach (var item in columns)
            {
                SelectListItem newSelectListItem = new SelectListItem { Text = item.Value, Value = item.Value, Selected = item.Value == selectedValue };
                searchColumns.Add(newSelectListItem);
            }
        }

        /// <summary>
        /// Following Action method takes ResponseId as a parameter and deletes the response.
        /// For now it returns nothing as a confirmation of deletion, we may add some error/success
        /// messages later. TBD
        /// </summary>
        /// <param name="responseId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Delete(string responseId)
        {
            SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
            surveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = responseId });
            string Id = Session[SessionKeys.UserId].ToString();
            surveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);
            surveyAnswerRequest.Criteria.IsSqlProject = (bool)Session[SessionKeys.IsSqlProject];
            surveyAnswerRequest.Criteria.SurveyId = Session[SessionKeys.RootFormId].ToString();
            surveyAnswerRequest.Criteria.StatusChangeReason = RecordStatusChangeReason.DeleteResponse;
            surveyAnswerRequest.Action = "Delete";
            SurveyAnswerResponse surveyAnswerResponse = _surveyFacade.DeleteResponse(surveyAnswerRequest);
            return Json(string.Empty);
        }

        public SurveyInfoModel GetSurveyInfo(string SurveyId)
        {
            SurveyInfoModel surveyInfoModel = _surveyFacade.GetSurveyInfoModel(SurveyId);
            return surveyInfoModel;
        }

        public List<FormInfoModel> GetFormsInfoList(Guid UserId, int OrgID)
        {
            FormsInfoRequest formReq = new FormsInfoRequest();

            formReq.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());//Hard coded user for now.
            formReq.Criteria.CurrentOrgId = OrgID;
            // formReq.Criteria.UserId = UserId;
            //define filter criteria here.
            //define sorting criteria here.

            List<FormInfoModel> listOfFormsInfoModel = _surveyFacade.GetFormsInfoModelList(formReq);

            // return listOfFormsInfoModel.Where(x=>x.OrganizationId== OrgID).ToList();
            return listOfFormsInfoModel;
        }

        public FormResponseInfoModel GetFormResponseInfoModel(string surveyId, int pageNumber, string sort = "", string sortfield = "", int orgid = -1)
        {
            // Initialize the Metadata Accessor
            MetadataAccessor.CurrentFormId = surveyId;

            FormResponseInfoModel formResponseInfoModel = null;

            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            if (!string.IsNullOrEmpty(surveyId))
            {
                formResponseInfoModel = GetFormResponseInfoModel(surveyId, orgid, userId);
                FormSettingResponse formSettingResponse = formResponseInfoModel.FormSettingResponse;

                var surveyResponseHelper = new SurveyResponseHelper();

                formResponseInfoModel.FormInfoModel.IsShared = formSettingResponse.FormInfo.IsShared;
                formResponseInfoModel.FormInfoModel.IsShareable = formSettingResponse.FormInfo.IsShareable;
                formResponseInfoModel.FormInfoModel.FormName = formSettingResponse.FormInfo.FormName;
                formResponseInfoModel.FormInfoModel.FormNumber = formSettingResponse.FormInfo.FormNumber;


                // Set User Role 
                //if (formResponseInfoModel.FormInfoModel.IsShared)
                //{

                //    SetUserRole(UserId, orgid);
                //}
                //else
                //{
                //SetUserRole(UserId, FormSettingResponse.FormInfo.OrganizationId);
                //}
                SetUserRole(userId, orgid);

                SurveyAnswerRequest formResponseReq = new SurveyAnswerRequest();
                formResponseReq.Criteria.SurveyId = surveyId.ToString();
                formResponseReq.Criteria.PageNumber = pageNumber;
                formResponseReq.Criteria.UserId = userId;
                formResponseReq.Criteria.IsSqlProject = formSettingResponse.FormInfo.IsSQLProject;
                formResponseReq.Criteria.IsShareable = formSettingResponse.FormInfo.IsShareable;
                formResponseReq.Criteria.UserOrganizationId = orgid;

                Session[SessionKeys.IsSqlProject] = formSettingResponse.FormInfo.IsSQLProject;
                Session[SessionKeys.IsOwner] = formSettingResponse.FormInfo.IsOwner;
                //if (Session[SessionKeys.SearchCriteria] != null)
                //{
                //    formResponseInfoModel.SearchModel = (SearchBoxModel)Session[SessionKeys.SearchCriteria];
                //}
                // Following code retain search starts
                if (Session[SessionKeys.SearchCriteria] != null &&
                    !string.IsNullOrEmpty(Session[SessionKeys.SearchCriteria].ToString()) &&
                    (Request.QueryString["col1"] == null || Request.QueryString["col1"] == "undefined"))
                {
                    formResponseReq.Criteria.SearchCriteria = Session[SessionKeys.SearchCriteria].ToString();
                    formResponseInfoModel.SearchModel = (SearchBoxModel)Session[SessionKeys.SearchModel];
                }
                else
                {
                    formResponseReq.Criteria.SearchCriteria = CreateSearchCriteria(Request.QueryString, formResponseInfoModel.SearchModel, formResponseInfoModel);
                    Session[SessionKeys.SearchModel] = formResponseInfoModel.SearchModel;
                    Session[SessionKeys.SearchCriteria] = formResponseReq.Criteria.SearchCriteria;
                }
                // Following code retain search ends
                PopulateDropDownlists(formResponseInfoModel, formSettingResponse.FormSetting.FormControlNameList.ToList());

                if (sort != null && sort.Length > 0)
                {
                    formResponseReq.Criteria.SortOrder = sort;
                }
                if (sortfield.Length > 0)
                {
                    formResponseReq.Criteria.Sortfield = sortfield;
                }

                formResponseReq.Criteria.SurveyQAList = Columns.ToDictionary(c => c.Key.ToString(), c => c.Value);
                formResponseReq.Criteria.FieldDigestList = formResponseInfoModel.ColumnDigests.ToDictionary(c => c.Key, c => c.Value);
                formResponseReq.Criteria.SearchDigestList = ToSearchDigestList(formResponseInfoModel.SearchModel, surveyId);


                SurveyAnswerResponse formResponseList = _surveyFacade.GetFormResponseList(formResponseReq);

                //foreach (var item in formResponseList.SurveyResponseList)
                //{
                //	SurveyAnswerDTO surveyAnswer = new SurveyAnswerDTO();
                //	surveyAnswer.IsLocked = false;
                //	surveyAnswer.ResponseId = item.ResponseId;
                //	//var pageResponseDetail = surveyAnswer.ResponseDetail.PageResponseDetailList.Where(p => p.PageNumber == criteria.PageNumber).SingleOrDefault();
                //	//if (pageResponseDetail == null)
                //	//{
                //	//    pageResponseDetail = new Cloud.Common.EntityObjects.PageResponseDetail() { PageNumber = criteria.PageNumber };
                //	//    surveyAnswer.ResponseDetail.AddPageResponseDetail(pageResponseDetail);
                //	//}
                //	//pageResponseDetail.ResponseQA = item.ResponseDetail != null ? item.ResponseDetail.FlattenedResponseQA() : new Dictionary<string, string>();
                //	surveyAnswer.ResponseDetail = item.ResponseDetail;
                //	formResponseList.SurveyResponseList.Add(surveyAnswer);
                //}

                //var ResponseTableList ; //= FormSettingResponse.FormSetting.DataRows;
                //Setting Resposes List
                List<ResponseModel> ResponseList = new List<ResponseModel>();
                List<ResponseModel> ResponseListModel = new List<ResponseModel>();
                Dictionary<string, string> dictory = new Dictionary<string, string>();
                List<Dictionary<string, string>> dictoryList = new List<Dictionary<string, string>>(); ;
                foreach (var item in formResponseList.SurveyResponseList)
                {
                    if (item.SqlData != null)
                    {
                        ResponseList.Add(ConvertRowToModel(item, Columns, "GlobalRecordId"));
                    }
                    else
                    {
                        //dictory = item.ResponseDetail.FlattenedResponseQA(key => key.ToLower());

                        //dictoryList.Add(dictory);// = new List<Dictionary<string, string>>();

                        ResponseList.Add(item.ToResponseModel(Columns));
                    }

                }

                if (!string.IsNullOrEmpty(sortfield))
                {
                    var sortList = ResponseList;
                    sortfield = sortfield.ToLower();
                    var columnValue = Columns.First(item => item.Value.ToLower() == sortfield);

                    for(int i=0;i<=5;i++)
                    {
                        var key = Columns[i].Value.ToLower();
                        if (key == sortfield)
                        {
                            ResponseListModel = sortList.OrderByDescending(x => x.Column1).ToList();
                            break;
                        }
                        if (key == sortfield)
                        {
                            ResponseListModel = sortList.OrderByDescending(x => x.Column2).ToList();
                            break;
                        }
                        if (key == sortfield)
                        {
                            ResponseListModel = sortList.OrderByDescending(x => x.Column3).ToList();
                            break;
                        }
                        if (key == sortfield)
                        {
                            ResponseListModel = sortList.OrderByDescending(x => x.Column4).ToList();
                            break;
                        }
                        if (key == sortfield)
                        {
                            ResponseListModel = sortList.OrderByDescending(x => x.Column5).ToList();
                            break;
                        }
                    }

                    formResponseInfoModel.ResponsesList = ResponseListModel;
                }
                else
                {
                    formResponseInfoModel.ResponsesList = ResponseList;
                }

                //Setting Form Info 
                formResponseInfoModel.FormInfoModel = formResponseList.FormInfo.ToFormInfoModel();
                //Setting Additional Data

                formResponseInfoModel.NumberOfPages = formResponseList.NumberOfPages;
                formResponseInfoModel.PageSize = ReadPageSize();
                formResponseInfoModel.NumberOfResponses = formResponseList.NumberOfResponses;
                formResponseInfoModel.sortfield = sortfield;
                formResponseInfoModel.sortOrder = sort;
                formResponseInfoModel.CurrentPage = pageNumber;
            }
            return formResponseInfoModel;
        }

        private void SetUserRole(int UserId, int OrgId)
        {
            UserRequest UserRequest = new UserRequest();
            UserRequest.Organization.OrganizationId = OrgId;
            UserRequest.User.UserId = UserId;
            var UserRes = _securityFacade.GetUserInfo(UserRequest);
            if (UserRes.User.Count() > 0)
            {
                Session[SessionKeys.UsertRole] = UserRes.User[0].Role;
            }
        }

        private void PopulateDropDownlists(FormResponseInfoModel FormResponseInfoModel, List<KeyValuePair<int, string>> list)
        {
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns1, FormResponseInfoModel.SearchModel.SearchCol1, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns2, FormResponseInfoModel.SearchModel.SearchCol2, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns3, FormResponseInfoModel.SearchModel.SearchCol3, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns4, FormResponseInfoModel.SearchModel.SearchCol4, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns5, FormResponseInfoModel.SearchModel.SearchCol5, list);
        }

        private int ReadPageSize()
        {
            return AppSettings.GetIntValue(AppSettings.Key.ResponsePageSize);
        }

        private SurveyAnswerDTO GetSurveyAnswer(string responseId, string formId)
        {
            SurveyAnswerDTO result = null;
            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            var SurveyAnswerResponse = _surveyFacade.GetSurveyAnswerResponse(responseId, formId, userId);
            result = SurveyAnswerResponse.SurveyResponseList[0];
            result.FormOwnerId = SurveyAnswerResponse.FormInfo.OwnerId;
            return result;
        }

        [HttpGet]
        public ActionResult LogOut()
        {

            FormsAuthentication.SignOut();
            this.Session.Clear();
            return RedirectToAction("Index", "Login");
        }

        [HttpGet]
        public ActionResult GetSettings(string formid)//List<FormInfoModel> ModelList, string formid)
        {
            FormSettingRequest FormSettingReq = new FormSettingRequest { ProjectId = Session[SessionKeys.ProjectId] as string };
            List<KeyValuePair<int, string>> TempColumns = new List<KeyValuePair<int, string>>();
            //Get All forms
            List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy(formid);
            // List<FormSettingResponse> FormSettingResponseList = new List<FormSettingResponse>();
            List<SettingsInfoModel> ModelList = new List<SettingsInfoModel>();
            foreach (var Item in FormsHierarchy)
            {
                FormSettingReq.GetMetadata = true;
                FormSettingReq.FormInfo.FormId = new Guid(Item.FormId).ToString();
                FormSettingReq.FormInfo.UserId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
                FormSettingReq.CurrentOrgId = int.Parse(Session[SessionKeys.SelectedOrgId].ToString());
                //Getting Column Name  List

                FormSettingResponse FormSettingResponse = _surveyFacade.GetFormSettings(FormSettingReq);
                //  FormSettingResponseList.Add(FormSettingResponse);

                // FormSettingResponse FormSettingResponse = _isurveyFacade.GetFormSettings(FormSettingReq);
                Columns = FormSettingResponse.FormSetting.ColumnNameList.ToList();
                TempColumns = Columns;
                Columns.Sort(Compare);

                Dictionary<int, string> dictionary = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                SettingsInfoModel Model = new SettingsInfoModel();
                Model.SelectedControlNameList = dictionary;

                Columns = FormSettingResponse.FormSetting.FormControlNameList.ToList();
                // Get Additional Metadata columns 
                if (!FormSettingResponse.FormInfo.IsSQLProject)
                {
                    var MetaDataColumns = Epi.Cloud.Common.Constants.Constant.MetaDaTaColumnNames();
                    Dictionary<int, string> Columndictionary = TempColumns.ToDictionary(pair => pair.Key, pair => pair.Value);

                    foreach (var item in MetaDataColumns)
                    {

                        if (!Columndictionary.ContainsValue(item))
                        {
                            Columns.Add(new KeyValuePair<int, string>(Columns.Count() + 1, item));
                        }

                    }

                    Columns.Sort(Compare);
                }

                Dictionary<int, string> dictionary1 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.FormControlNameList = dictionary1;

                Columns = FormSettingResponse.FormSetting.AssignedUserList.ToList();
                if (Columns.Exists(col => col.Value == Session[SessionKeys.UserEmailAddress].ToString()))
                {
                    Columns.Remove(Columns.First(u => u.Value == Session[SessionKeys.UserEmailAddress].ToString()));
                }

                //Columns.Sort(Compare);

                Dictionary<int, string> dictionary2 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.AssignedUserList = dictionary2;

                Columns = FormSettingResponse.FormSetting.UserList.ToList();

                if (Columns.Exists(col => col.Value == Session[SessionKeys.UserEmailAddress].ToString()))
                {
                    Columns.Remove(Columns.First(u => u.Value == Session[SessionKeys.UserEmailAddress].ToString()));
                }
                //Columns.Sort(Compare);

                Dictionary<int, string> dictionary3 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.UserList = dictionary3;

                Columns = FormSettingResponse.FormSetting.AvailableOrgList.ToList();
                Dictionary<int, string> dictionary4 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                Model.AvailableOrgList = dictionary4;

                Columns = FormSettingResponse.FormSetting.SelectedOrgList.ToList();
                Dictionary<int, string> dictionary5 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                Model.SelectedOrgList = dictionary5;

                Model.IsShareable = FormSettingResponse.FormInfo.IsShareable;
                Model.IsDraftMode = FormSettingResponse.FormInfo.IsDraftMode;
                Model.FormOwnerFirstName = FormSettingResponse.FormInfo.OwnerFName;
                Model.FormOwnerLastName = FormSettingResponse.FormInfo.OwnerLName;
                Model.FormName = FormSettingResponse.FormInfo.FormName;
                Model.FormId = Item.FormId;
                Model.DataAccessRuleIds = FormSettingResponse.FormSetting.DataAccessRuleIds;
                Model.SelectedDataAccessRule = FormSettingResponse.FormSetting.SelectedDataAccessRule;
                Model.HasDraftModeData = FormSettingResponse.FormInfo.HasDraftModeData;
                var DataAccessRuleDescription = "";
                foreach (var item in FormSettingResponse.FormSetting.DataAccessRuleDescription)
                {
                    DataAccessRuleDescription = DataAccessRuleDescription + item.Key.ToString() + " : " + item.Value + "\n";
                }

                Model.DataAccessRuleDescription = DataAccessRuleDescription;
                ModelList.Add(Model);
            }
            return PartialView("Settings", ModelList);
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CheckForConcurrency(String responseId)
        {
            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            var surveyAnswerStateDTO = GetSurveyAnswerState(responseId);
            if (surveyAnswerStateDTO.DateCreated == DateTime.MinValue) surveyAnswerStateDTO.DateCreated = surveyAnswerStateDTO.DateUpdated;
            surveyAnswerStateDTO.LoggedInUserId = userId;
            Session[SessionKeys.EditForm] = responseId;

            // Minimize the amount of Json data by serializing only pertinent state information
            var json = Json(surveyAnswerStateDTO.ToSurveyAnswerDTO());
            return json;
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Notify(String ResponseId)
        {
            int UserId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());

            //Get current user info
            int CurrentOrgId = int.Parse(Session[SessionKeys.SelectedOrgId].ToString());
            var UserInfo = _securityFacade.GetUserInfo(UserId);
            //Get Organization admin info 
            var surveyAnswerDTO = GetSurveyAnswer(ResponseId, Session[SessionKeys.RootFormId].ToString());
            SurveyInfoModel surveyInfoModel = GetSurveyInfo(surveyAnswerDTO.SurveyId);

            var OwnerInfo = _securityFacade.GetUserInfo(surveyAnswerDTO.FormOwnerId);

            var email = new Email();
            //ResponseId;

            email.Subject = "Record locked notification.";
            email.Body = " A user was unable to edit/delete a Epi Info™ Cloud Enter recored. \n \n Please login to Epi Info™ Cloud Enter system to Unlock this record.\n \n Below are the needed info to unlock the record.\n \n Response id: " + ResponseId + "\n\n User email: " + UserInfo.User.EmailAddress + "\n\n";
            email.From = ConfigurationManager.AppSettings["EMAIL_FROM"];
            email.To = new List<string>();
            email.To.Add(OwnerInfo.User.EmailAddress);

            var success = EmailHandler.SendMessage(email);

            return Json(1);
        }

        //Unlock
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Unlock(String ResponseId, bool RecoverLastRecordVersion = false)
        {
            try
            {
                SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
                SurveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
                SurveyAnswerRequest.Criteria.StatusId = RecordStatus.Saved;
                SurveyAnswerRequest.Criteria.SurveyAnswerIdList.Add(ResponseId);
                Session[SessionKeys.RecoverLastRecordVersion] = RecoverLastRecordVersion;
                //  _isurveyFacade.UpdateResponseStatus(SurveyAnswerRequest);
            }
            catch (Exception ex)
            {
                return Json("Erorr");
            }
            return Json("Success");
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SaveSettings(string formid)
        {
            List<FormsHierarchyDTO> FormList = GetFormsHierarchy(formid);
            FormSettingRequest FormSettingReq = new FormSettingRequest { ProjectId = Session[SessionKeys.ProjectId] as string };
            int UserId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            foreach (var Form in FormList)
            {
                FormSettingReq.GetMetadata = true;
                FormSettingReq.FormInfo.FormId = new Guid(formid).ToString();
                FormSettingReq.FormInfo.UserId = UserId;
                FormSettingDTO FormSetting = new FormSettingDTO();
                FormSetting.FormId = Form.FormId;
                FormSetting.ColumnNameList = GetDictionary(this.Request.Form["SelectedColumns_" + Form.FormId]);
                FormSetting.AssignedUserList = GetDictionary(this.Request.Form["SelectedUser"]);
                FormSetting.SelectedOrgList = GetDictionary(this.Request.Form["SelectedOrg"]);
                FormSetting.IsShareable = GetBoolValue(this.Request.Form["IsShareable"]);
                FormSetting.SelectedDataAccessRule = int.Parse(this.Request.Form["DataAccessRuleId"]);

                if (!string.IsNullOrEmpty(this.Request.Form["SoftDeleteForm"]) && this.Request.Form["SoftDeleteForm"].ToUpper() == "ON")
                {
                    FormSetting.IsDisabled = true;
                }
                if (!string.IsNullOrEmpty(this.Request.Form["RemoveTestData"]) && this.Request.Form["RemoveTestData"].ToUpper() == "ON")
                {
                    FormSetting.DeleteDraftData = true;
                }
                FormSettingReq.FormSetting.Add(FormSetting);
                FormSettingReq.FormInfo.IsDraftMode = GetBoolValue(this.Request.Form["Mode"]);

            }
            FormSettingResponse FormSettingResponse = _surveyFacade.SaveSettings(FormSettingReq);

            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

            var model = new FormResponseInfoModel();

            int CurrentOrgId = int.Parse(Session[SessionKeys.SelectedOrgId].ToString());
            model = GetFormResponseInfoModel(formid, 1, "", "", CurrentOrgId);

            if (IsMobileDevice == false)
            {
                if (!string.IsNullOrEmpty(this.Request.Form["SoftDeleteForm"]) && this.Request.Form["SoftDeleteForm"].ToUpper() == "ON")
                {
                    return Json(null);
                }
                else
                {
                    return PartialView("ListResponses", model);
                }
            }
            else
            {
                return View("ListResponses", model);
            }
        }

        private Dictionary<int, string> GetDictionary(string List)
        {
            Dictionary<int, string> Dictionary = new Dictionary<int, string>();
            if (!string.IsNullOrEmpty(List))
            {
                Dictionary = List.Split(',').ToList().Select((s, i) => new { s, i }).ToDictionary(x => x.i, x => x.s);
            }
            return Dictionary;
        }

        private bool GetBoolValue(string value)
        {
            bool BoolValue = false;
            if (!string.IsNullOrEmpty(value))
            {
                int val = int.Parse(value);
                if (val == 1)
                {
                    BoolValue = true;
                }
            }

            return BoolValue;
        }


        private List<FormsHierarchyDTO> GetFormsHierarchy(string formid)
        {
            FormsHierarchyResponse FormsHierarchyResponse = new FormsHierarchyResponse();
            FormsHierarchyRequest FormsHierarchyRequest = new FormsHierarchyRequest();

            FormsHierarchyRequest.SurveyInfo.FormId = formid;
            // FormsHierarchyRequest.SurveyResponseInfo.ResponseId = Session[SessionKeys.RootResponseId].ToString();
            FormsHierarchyResponse = _surveyFacade.GetFormsHierarchy(FormsHierarchyRequest);

            return FormsHierarchyResponse.FormsHierarchy;
        }
    }
}
