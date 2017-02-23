﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Security;
using Epi.Cloud.Common.Constants;
using Epi.Cloud.Common.DTO;
using Epi.Cloud.Common.Extensions;
using Epi.Cloud.Common.Message;
using Epi.Cloud.Common.Metadata;
using Epi.Cloud.Common.Model;
using Epi.Cloud.Facades.Interfaces;
using Epi.Cloud.Interfaces.MetadataInterfaces;
using Epi.Cloud.MVC.Extensions;
using Epi.Core.EnterInterpreter;
using Epi.DataPersistence.Constants;
using Epi.DataPersistence.DataStructures;
using Epi.FormMetadata.DataStructures;
using Epi.Web.MVC.Models;
using Epi.Web.MVC.Utility;

namespace Epi.Web.MVC.Controllers
{
    [Authorize]
    public class FormResponseController : BaseSurveyController
    {
        private IEnumerable<AbridgedFieldInfo> _pageFields;
        private string _requiredList = "";
        private bool _isNewRequest = true; //Added for retain search and sort

        public FormResponseController(ISurveyFacade isurveyFacade,
                                      IProjectMetadataProvider projectMetadataProvider
)
        {
            _surveyFacade = isurveyFacade;
            _projectMetadataProvider = projectMetadataProvider;
        }

        [HttpGet]
        //string responseid,string SurveyId, int ViewId, string CurrentPage
        // View =0 Root form
        public ActionResult Index(string formid, string responseid, int pagenumber = 1, int viewId = 0)
        {
            bool reset = false;
            bool.TryParse(Request.QueryString["reset"], out reset);
            if (reset)
            {
                Session[SessionKeys.SortOrder] = "";
                Session[SessionKeys.SortField] = "";
            }
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            ViewBag.Version = version;
            bool isAndroid = false;

            if (this.Request.UserAgent.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isAndroid = true;
            }
            if (viewId == 0)
            {
                //Following code checks if request is for new or selected form.
                if (Session[SessionKeys.RootFormId] != null &&
                    Session[SessionKeys.RootFormId].ToString() == formid)
                {
                    _isNewRequest = false;
                }

                Session[SessionKeys.RootFormId] = formid;
                Session.Remove(SessionKeys.RootResponseId);
                Session.Remove(SessionKeys.FormValuesHasChanged);
                Session[SessionKeys.IsEditMode] = false;

                var model = new FormResponseInfoModel();
                model.ViewId = viewId;
                model = GetSurveyResponseInfoModel(formid, pagenumber, null, null, -1);
                Session[SessionKeys.SelectedOrgId] = model.FormInfoModel.OrganizationId;
                return View("Index", model);
            }
            else
            {
                List<FormsHierarchyDTO> formsHierarchy = GetFormsHierarchy();
                int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());

                bool isMobileDevice = this.Request.Browser.IsMobileDevice;

                int requestedViewId = viewId;

                Session[SessionKeys.RequestedViewId] = requestedViewId;
                SurveyModel surveyModel = new SurveyModel();

                surveyModel.RelateModel = formsHierarchy.ToRelateModel(formid);
                surveyModel.RequestedViewId = requestedViewId;

                var relateSurveyId = formsHierarchy.Single(x => x.ViewId == viewId);

                if (!string.IsNullOrEmpty(responseid))
                {
                    //SurveyModel.FormResponseInfoModel = GetFormResponseInfoModel(RelateSurveyId.FormId, responseid, Pagenumber, null, null);
                    surveyModel.FormResponseInfoModel = GetFormResponseInfoModels(relateSurveyId.FormId, responseid, formsHierarchy);
                    surveyModel.FormResponseInfoModel.NumberOfResponses = surveyModel.FormResponseInfoModel.ResponsesList.Count();

                    surveyModel.FormResponseInfoModel.ParentResponseId = responseid;
                }

                if (relateSurveyId.ResponseIds.Count() > 0)
                {
                    SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(relateSurveyId.ResponseIds[0].ResponseId);
                    var form = _surveyFacade.GetSurveyFormData(relateSurveyId.ResponseIds[0].SurveyId, 1, surveyAnswerDTO, isMobileDevice, null, null, isAndroid);
                    surveyModel.Form = form;
                    if (string.IsNullOrEmpty(responseid))
                    {
                        surveyModel.FormResponseInfoModel = GetFormResponseInfoModels(relateSurveyId.FormId, responseid, formsHierarchy);
                        //SurveyModel.FormResponseInfoModel = GetFormResponseInfoModel(RelateSurveyId.FormId, RelateSurveyId.ResponseIds[0].RelateParentId, Pagenumber, null, null);
                        surveyModel.FormResponseInfoModel.ParentResponseId = relateSurveyId.ResponseIds[0].RelateParentId;
                    }

                    surveyModel.FormResponseInfoModel.FormInfoModel.FormName = form.SurveyInfo.SurveyName;
                    surveyModel.FormResponseInfoModel.FormInfoModel.FormId = form.SurveyInfo.SurveyId;
                    surveyModel.FormResponseInfoModel.NumberOfResponses = surveyModel.FormResponseInfoModel.ResponsesList.Count();
                }
                else
                {
                    FormResponseInfoModel responseInfoModel = new FormResponseInfoModel();
                    if (surveyModel.FormResponseInfoModel.ResponsesList.Count() > 0)
                    {
                        SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(surveyModel.FormResponseInfoModel.ResponsesList[0].Column0, relateSurveyId.FormId);
                        responseInfoModel = GetFormResponseInfoModels(relateSurveyId.FormId, responseid, formsHierarchy);
                        //responseInfoModel = GetFormResponseInfoModel(RelateSurveyId.FormId, responseid, Pagenumber, null, null);
                        surveyModel.Form = _surveyFacade.GetSurveyFormData(surveyAnswerDTO.SurveyId, 1, surveyAnswerDTO, isMobileDevice, null, null, isAndroid);
                        responseInfoModel.FormInfoModel.FormName = surveyModel.Form.SurveyInfo.SurveyName.ToString();
                        responseInfoModel.FormInfoModel.FormId = surveyModel.Form.SurveyInfo.SurveyId.ToString();
                        responseInfoModel.ParentResponseId = responseid;//SurveyModel.FormResponseInfoModel.ResponsesList[0].Column0;
                        responseInfoModel.NumberOfResponses = surveyModel.FormResponseInfoModel.ResponsesList.Count();
                    }
                    else
                    {
                        var form1 = _surveyFacade.GetSurveyInfoModel(relateSurveyId.FormId);
                        responseInfoModel.FormInfoModel.FormName = form1.SurveyName.ToString();
                        responseInfoModel.FormInfoModel.FormId = form1.SurveyId.ToString();
                        responseInfoModel.ParentResponseId = responseid;
                    }
                    surveyModel.FormResponseInfoModel = responseInfoModel;
                }
                surveyModel.FormResponseInfoModel.ViewId = viewId;

                return View("Index", surveyModel.FormResponseInfoModel);
            }
        }

        [HttpPost]
        public ActionResult Index(string surveyId, string addNewFormId, string editForm, string cancel)
        {

            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            string userName = Session[SessionKeys.UserName].ToString();
            bool isMobileDevice = this.Request.Browser.IsMobileDevice;
            FormsAuthentication.SetAuthCookie("BeginSurvey", false);
            bool isEditMode = false;
            if (isMobileDevice == true)
            {
                isMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
            }
            bool isAndroid = this.Request.UserAgent.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!string.IsNullOrEmpty(/*FromURL*/cancel))
            {
                int pageNumber;
                int.TryParse(/*FromURL*/cancel, out pageNumber);
                Dictionary<string, int> surveyPagesList = (Dictionary<string, int>)Session[SessionKeys.RelateButtonPageId];
                if (surveyPagesList != null)
                {
                    pageNumber = surveyPagesList[this.Request.Form["Parent_Response_Id"].ToString()];
                }

                return RedirectToRoute(new { Controller = "Survey", Action = "Index", responseid = this.Request.Form["Parent_Response_Id"].ToString(), PageNumber = pageNumber });
            }
            if (string.IsNullOrEmpty(/*FromURL*/editForm) && Session[SessionKeys.EditForm] != null && string.IsNullOrEmpty(/*FromURL*/addNewFormId))
            {
                /*FromURL*/editForm = Session[SessionKeys.EditForm].ToString();
            }

            var editFormResponseId = /*FromURL*/editForm;

            if (!string.IsNullOrEmpty(editFormResponseId))
            {
                //Session[SessionKeys.RootFormId] = surveyid;
                if (Session[SessionKeys.RootResponseId] == null)
                {
                    Session[SessionKeys.RootResponseId] = editFormResponseId;
                }
                Session[SessionKeys.IsEditMode] = true;
                isEditMode = true;
                SurveyAnswerDTO surveyAnswer = GetSurveyAnswer(editFormResponseId);
                if (Session[SessionKeys.RecoverLastRecordVersion] != null)
                {
                    surveyAnswer.RecoverLastRecordVersion = bool.Parse(Session[SessionKeys.RecoverLastRecordVersion].ToString());
                }
                string childRecordId = GetChildRecordId(surveyAnswer);
                return RedirectToAction(ViewActions.Index, ControllerNames.Survey, new { responseid = surveyAnswer.ParentRecordId, PageNumber = 1, Edit = "Edit" });
            }

            //create the responseid
            Guid responseID = Guid.NewGuid();
            if (Session[SessionKeys.RootResponseId] == null)
            {
                Session[SessionKeys.RootResponseId] = responseID;
            }
            TempData[TempDataKeys.ResponseId] = responseID.ToString();

            // create the first survey response
            SurveyAnswerDTO surveyAnswerDTO = _surveyFacade.CreateSurveyAnswer(/*FromURL*/addNewFormId, responseID.ToString(), userId, true, this.Request.Form["Parent_Response_Id"].ToString(), isEditMode);
            List<FormsHierarchyDTO> formsHierarchy = GetFormsHierarchy();
            SurveyInfoModel surveyInfoModel = GetSurveyInfo(surveyAnswerDTO.SurveyId, formsHierarchy);
            MetadataAccessor metadataAccessor = surveyInfoModel as MetadataAccessor;

            // set the survey answer to be production or test 
            surveyAnswerDTO.IsDraftMode = surveyInfoModel.IsDraftMode;

            MvcDynamicForms.Form form = _surveyFacade.GetSurveyFormData(surveyAnswerDTO.SurveyId, 1, surveyAnswerDTO, isMobileDevice, null, formsHierarchy, isAndroid);

            TempData[TempDataKeys.Width] = form.Width + 100;

            var formDigest = metadataAccessor.GetFormDigest(surveyAnswerDTO.SurveyId);
            string checkcode = formDigest.CheckCode;

            FormResponseDetail responseDetail = surveyAnswerDTO.ResponseDetail;

            form.FormCheckCodeObj = form.GetCheckCodeObj(MetadataAccessor.GetFieldDigests(surveyAnswerDTO.SurveyId), responseDetail, checkcode);

            ///////////////////////////// Execute - Record Before - start//////////////////////
            Dictionary<string, string> ContextDetailList = new Dictionary<string, string>();
            EnterRule functionObject_B = (EnterRule)form.FormCheckCodeObj.GetCommand("level=record&event=before&identifier=");
            SurveyResponseHelper surveyResponseDocDb = new SurveyResponseHelper(_pageFields, _requiredList);
            if (functionObject_B != null && !functionObject_B.IsNull())
            {
                try
                {
                    PageDigest[] pageDigests = form.MetadataAccessor.GetCurrentFormPageDigests();
                    responseDetail = surveyResponseDocDb.CreateResponseDocument(pageDigests);

                    Session[SessionKeys.RequiredList] = surveyResponseDocDb.RequiredList;
                    _requiredList = surveyResponseDocDb.RequiredList;
                    form.RequiredFieldsList = _requiredList;
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


                    ContextDetailList = SurveyHelper.GetContextDetailList(functionObject_B);
                    form = SurveyHelper.UpdateControlsValuesFromContext(form, ContextDetailList);

                    _surveyFacade.UpdateSurveyResponse(surveyInfoModel, responseID.ToString(), form, surveyAnswerDTO, false, false, 0, userId, userName);
                }
                catch (Exception ex)
                {
                    // do nothing so that processing
                    // can continue
                }
            }
            else
            {
                PageDigest[] pageDigestArray = form.MetadataAccessor.GetCurrentFormPageDigests();// metadataAccessor.GetPageDigests(surveyInfoModel.SurveyId);

                surveyAnswerDTO.ResponseDetail = surveyResponseDocDb.CreateResponseDocument(pageDigestArray);

                _requiredList = surveyResponseDocDb.RequiredList;
                Session[SessionKeys.RequiredList] = surveyResponseDocDb.RequiredList;
                form.RequiredFieldsList = _requiredList;
                // Session[SessionKeys.RequiredList] = _requiredList;
                _surveyFacade.UpdateSurveyResponse(surveyInfoModel, surveyAnswerDTO.ResponseId, form, surveyAnswerDTO, false, false, 0, userId, userName);
            }

            surveyAnswerDTO = (SurveyAnswerDTO)formsHierarchy.SelectMany(x => x.ResponseIds).FirstOrDefault(z => z.ResponseId == surveyAnswerDTO.ResponseId);

            ///////////////////////////// Execute - Record Before - End//////////////////////
            return RedirectToAction(ViewActions.Index, ControllerNames.Survey, new { responseid = responseID, PageNumber = 1 });
        }


        public FormResponseInfoModel GetSurveyResponseInfoModel(string surveyId, int pageNumber, string sort = "", string sortfield = "", int orgid = -1)
        {
            // Initialize the Metadata Accessor
            MetadataAccessor.CurrentFormId = surveyId;

            FormResponseInfoModel formResponseInfoModel = null;

            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            if (!string.IsNullOrEmpty(surveyId))
            {
                formResponseInfoModel = GetFormResponseInfoModel(surveyId, orgid, userId);
                FormSettingResponse formSettingResponse = formResponseInfoModel.FormSettingResponse;
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
                //SetUserRole(userId, orgid);

                SurveyAnswerRequest formResponseReq = new SurveyAnswerRequest();
                formResponseReq.Criteria.SurveyId = surveyId.ToString();
                formResponseReq.Criteria.PageNumber = /*FromURL*/pageNumber;
                formResponseReq.Criteria.UserId = userId;
                formResponseReq.Criteria.IsSqlProject = formSettingResponse.FormInfo.IsSQLProject;
                formResponseReq.Criteria.IsShareable = formSettingResponse.FormInfo.IsShareable;
                formResponseReq.Criteria.IsMobile = true;
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

                if (!string.IsNullOrWhiteSpace(/*FromURL*/sort))
                {
                    formResponseReq.Criteria.SortOrder = sort;
                }
                if (!string.IsNullOrWhiteSpace(/*FromURL*/sortfield))
                {
                    formResponseReq.Criteria.Sortfield = /*FromURL*/sortfield;
                }
                formResponseReq.Criteria.SurveyQAList = _columns.ToDictionary(c => c.Key.ToString(), c => c.Value);
                formResponseReq.Criteria.FieldDigestList = formResponseInfoModel.ColumnDigests.ToDictionary(c => c.Key, c => c.Value);
                formResponseReq.Criteria.SearchDigestList = ToSearchDigestList(formResponseInfoModel.SearchModel, surveyId);


                SurveyAnswerResponse formResponseList = _surveyFacade.GetFormResponseList(formResponseReq);

                //foreach (var item in formResponseList.SurveyResponseList)
                //{
                //   SurveyAnswerDTO surveyAnswer = new SurveyAnswerDTO();
                //   surveyAnswer.IsLocked = false;
                //   surveyAnswer.ResponseId = item.ResponseId;
                //   //var pageResponseDetail = surveyAnswer.ResponseDetail.PageResponseDetailList.Where(p => p.PageNumber == criteria.PageNumber).SingleOrDefault();
                //   //if (pageResponseDetail == null)
                //   //{
                //   //    pageResponseDetail = new Cloud.Common.EntityObjects.PageResponseDetail() { PageNumber = criteria.PageNumber };
                //   //    surveyAnswer.ResponseDetail.AddPageResponseDetail(pageResponseDetail);
                //   //}
                //   //pageResponseDetail.ResponseQA = item.ResponseDetail != null ? item.ResponseDetail.FlattenedResponseQA() : new Dictionary<string, string>();
                //   surveyAnswer.ResponseDetail = item.ResponseDetail;
                //   formResponseList.SurveyResponseList.Add(surveyAnswer);
                //}

                //var ResponseTableList ; //= FormSettingResponse.FormSetting.DataRows;
                //Setting Resposes List
                List<ResponseModel> ResponseList = new List<ResponseModel>();
                foreach (var item in formResponseList.SurveyResponseList)
                {
                    if (item.SqlData != null)
                    {
                        ResponseList.Add(ConvertRowToModel(item, _columns, "GlobalRecordId"));
                    }
                    else
                    {
                        ResponseList.Add(item.ToResponseModel(_columns));
                    }
                }

                formResponseInfoModel.ResponsesList = ResponseList;
                //Setting Form Info 
                formResponseInfoModel.FormInfoModel = formResponseList.FormInfo.ToFormInfoModel();
                //Setting Additional Data

                formResponseInfoModel.NumberOfPages = formResponseList.NumberOfPages;
                formResponseInfoModel.PageSize = ReadPageSize();
                formResponseInfoModel.NumberOfResponses = formResponseList.NumberOfResponses;
                formResponseInfoModel.sortfield = /*FromURL*/sortfield;
                formResponseInfoModel.sortOrder = /*FromURL*/sort;
                formResponseInfoModel.CurrentPage = /*FromURL*/pageNumber;
            }
            return formResponseInfoModel;
        }


        private void PopulateDropDownlists(FormResponseInfoModel formResponseInfoModel, List<KeyValuePair<int, string>> list)
        {
            PopulateDropDownlist(out formResponseInfoModel.SearchColumns1, formResponseInfoModel.SearchModel.SearchCol1, list);
            PopulateDropDownlist(out formResponseInfoModel.SearchColumns2, formResponseInfoModel.SearchModel.SearchCol2, list);
            PopulateDropDownlist(out formResponseInfoModel.SearchColumns3, formResponseInfoModel.SearchModel.SearchCol3, list);
            PopulateDropDownlist(out formResponseInfoModel.SearchColumns4, formResponseInfoModel.SearchModel.SearchCol4, list);
            PopulateDropDownlist(out formResponseInfoModel.SearchColumns5, formResponseInfoModel.SearchModel.SearchCol5, list);
        }

        public SurveyInfoModel GetSurveyInfo(string surveyId, List<FormsHierarchyDTO> formsHierarchyDTOList = null)
        {
            SurveyInfoModel surveyInfoModel = new SurveyInfoModel();
            if (formsHierarchyDTOList != null)
            {
                var formsHierarchyDTO = formsHierarchyDTOList.FirstOrDefault(x => x.FormId == surveyId).SurveyInfo;
                surveyInfoModel = formsHierarchyDTO.ToSurveyInfoModel();
            }
            else
            {
                surveyInfoModel = _surveyFacade.GetSurveyInfoModel(surveyId);
            }
            return surveyInfoModel;
        }

        private string GetChildRecordId(SurveyAnswerDTO surveyAnswerDTO)
        {
            SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
            SurveyAnswerResponse SurveyAnswerResponse = new SurveyAnswerResponse();
            string ChildId = Guid.NewGuid().ToString();
            surveyAnswerDTO.ParentRecordId = surveyAnswerDTO.ResponseId;
            surveyAnswerDTO.ResponseId = ChildId;
            surveyAnswerDTO.Status = RecordStatus.InProcess;
            surveyAnswerDTO.ReasonForStatusChange = RecordStatusChangeReason.CreateMulti;
            SurveyAnswerRequest.SurveyAnswerList.Add(surveyAnswerDTO);
            string result;

            //responseId = TempData[TempDataKeys.ResponseId].ToString();
            string Id = Session[SessionKeys.UserId].ToString();
            SurveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);//_UserId;
            SurveyAnswerRequest.RequestId = ChildId;
            SurveyAnswerRequest.Action = RequestAction.CreateMulti;
            SurveyAnswerResponse = _surveyFacade.SetChildRecord(SurveyAnswerRequest);
            result = SurveyAnswerResponse.SurveyResponseList[0].ResponseId.ToString();
            return result;
        }

        private SurveyAnswerDTO GetSurveyAnswer(string responseId, string formId = "")
        {
            SurveyAnswerDTO result = null;

            string rootFormId = Session[SessionKeys.RootFormId].ToString();
            string userId = Session[SessionKeys.UserId].ToString();
            if (string.IsNullOrEmpty(formId))
            {
                result = _surveyFacade.GetSurveyAnswerResponse(responseId, rootFormId, SurveyHelper.GetDecryptUserId(userId)).SurveyResponseList[0];
            }
            else
            {
                result = _surveyFacade.GetSurveyAnswerResponse(responseId, formId, SurveyHelper.GetDecryptUserId(userId)).SurveyResponseList[0];
            }
            return result;
        }


        /// <summary>
        /// Following Action method takes ResponseId as a parameter and deletes the response.
        /// For now it returns nothing as a confirmation of deletion, we may add some error/success
        /// messages later. TBD
        /// </summary>
        /// <param name="ResponseId"></param>
        /// <returns></returns>
        [HttpPost]
        //public ActionResult Delete(string ResponseId, string surveyid)
        //{
        //    SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
        //    surveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
        //    string Id = Session[SessionKeys.UserId].ToString();
        //    surveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);
        //    surveyAnswerRequest.Criteria.IsEditMode = false;
        //    surveyAnswerRequest.Criteria.IsDeleteMode = false;
        //    surveyAnswerRequest.Criteria.IsSqlProject = (bool)Session[SessionKeys.IsSqlProject];
        //    surveyAnswerRequest.Criteria.SurveyId = Session[SessionKeys.RootFormId].ToString();
        //    surveyAnswerRequest.Action = RequestAction.Delete;
        //    SurveyAnswerResponse SAResponse = _surveyFacade.DeleteResponse(surveyAnswerRequest);

        //    return Json(surveyid);
        //}

        //Mobile record Delete
        public ActionResult Delete(string responseId)
        {
            SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
            surveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = responseId });
            string Id = Session[SessionKeys.UserId].ToString();
            surveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);
            surveyAnswerRequest.Criteria.IsSqlProject = (bool)Session[SessionKeys.IsSqlProject];
            surveyAnswerRequest.Criteria.SurveyId = Session[SessionKeys.RootFormId].ToString();
            surveyAnswerRequest.Criteria.StatusChangeReason = RecordStatusChangeReason.DeleteResponse;
            surveyAnswerRequest.Criteria.StatusChangeReason = RecordStatusChangeReason.DeleteResponse;
            surveyAnswerRequest.Action = RequestAction.Delete;
            SurveyAnswerResponse surveyAnswerResponse = _surveyFacade.DeleteResponse(surveyAnswerRequest);
            return Json(string.Empty);
        }

        [HttpPost]
        public ActionResult DeleteBranch(string ResponseId)//List<FormInfoModel> ModelList, string formid)
        {

            SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
            surveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
            surveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            surveyAnswerRequest.Criteria.IsEditMode = false;
            surveyAnswerRequest.Criteria.IsDeleteMode = false;
            surveyAnswerRequest.Criteria.IsSqlProject = (bool)Session[SessionKeys.IsSqlProject];
            surveyAnswerRequest.Criteria.SurveyId = Session[SessionKeys.RootFormId].ToString();
            SurveyAnswerResponse saResponse = _surveyFacade.DeleteResponse(surveyAnswerRequest);

            return Json(string.Empty);
        }

        private List<FormsHierarchyDTO> GetFormsHierarchy()
        {
            FormsHierarchyResponse formsHierarchyResponse = new FormsHierarchyResponse();
            FormsHierarchyRequest formsHierarchyRequest = new FormsHierarchyRequest();
            if (Session[SessionKeys.RootFormId] != null && Session[SessionKeys.RootResponseId] != null)
            {
                formsHierarchyRequest.SurveyInfo.FormId = Session[SessionKeys.RootFormId].ToString();
                formsHierarchyRequest.SurveyResponseInfo.ResponseId = Session[SessionKeys.RootResponseId].ToString();
                formsHierarchyResponse = _surveyFacade.GetFormsHierarchy(formsHierarchyRequest);
            }
            return formsHierarchyResponse.FormsHierarchy;
        }

        private FormResponseInfoModel GetFormResponseInfoModels(string surveyId, string responseId, List<FormsHierarchyDTO> formsHierarchyDTOList = null)
        {
            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            FormResponseInfoModel formResponseInfoModel = new FormResponseInfoModel();

            var formHieratchyDTO = formsHierarchyDTOList.FirstOrDefault(h => h.FormId == surveyId);

            SurveyResponseHelper surveyResponseHelper = new SurveyResponseHelper();
            if (!string.IsNullOrEmpty(surveyId))
            {
                SurveyAnswerRequest surveyAnswerRequest = new SurveyAnswerRequest();
                FormSettingRequest formSettingRequest = new FormSettingRequest { ProjectId = Session[SessionKeys.ProjectId] as string };

                //Populating the request

                formSettingRequest.FormInfo.FormId = surveyId;
                formSettingRequest.FormInfo.UserId = userId;
                //Getting Column Name  List
                FormSettingResponse formSettingResponse = _surveyFacade.GetFormSettings(formSettingRequest);
                _columns = formSettingResponse.FormSetting.ColumnNameList.ToList();
                _columns.Sort(Compare);

                // Setting  Column Name  List
                formResponseInfoModel.Columns = _columns;

                //Getting Resposes
                var ResponseListDTO = formsHierarchyDTOList.FirstOrDefault(x => x.FormId == surveyId).ResponseIds;

                // If we don't have any data for this child form yet then create a response 
                if (ResponseListDTO.Count == 0)
                {
                    var surveyAnswerDTO = new SurveyAnswerDTO();
                    surveyAnswerDTO.CurrentPageNumber = 1;
                    surveyAnswerDTO.DateUpdated = DateTime.UtcNow;
                    surveyAnswerDTO.RelateParentId = responseId;
                    surveyAnswerDTO.ResponseId = Guid.NewGuid().ToString();
                    surveyAnswerDTO.ResponseDetail = new FormResponseDetail();
                    ResponseListDTO.Add(surveyAnswerDTO);
                }

                //Setting Resposes List
                List<ResponseModel> ResponseList = new List<ResponseModel>();
                foreach (var item in ResponseListDTO)
                {
                    if (item.RelateParentId == responseId)
                    {
                        if (item.SqlData != null)
                        {
                            ResponseList.Add(ConvertRowToModel(item, _columns, "ChildGlobalRecordID"));
                        }
                        else
                        {
                            ResponseList.Add(item.ToResponseModel(_columns));
                        }
                    }
                }

                formResponseInfoModel.ResponsesList = ResponseList;

                formResponseInfoModel.PageSize = ReadPageSize();

                formResponseInfoModel.CurrentPage = 1;
            }
            return formResponseInfoModel;
        }

        private int ReadPageSize()
        {
            return Convert.ToInt16(WebConfigurationManager.AppSettings["RESPONSE_PAGE_SIZE"].ToString());
        }

        [HttpGet]
        public ActionResult LogOut()
        {
            FormsAuthentication.SignOut();
            this.Session.Clear();
            return RedirectToAction(ViewActions.Index, ControllerNames.Login);
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CheckForConcurrency(String responseId)
        {
            int userId = SurveyHelper.GetDecryptUserId(Session[SessionKeys.UserId].ToString());
            var surveyAnswerStateDTO = GetSurveyAnswerState(responseId, Session[SessionKeys.RootFormId].ToString());
            surveyAnswerStateDTO.LoggedInUserId = userId;
            Session[SessionKeys.EditForm] = responseId;

            // Minimize the amount of Json data by serializing only pertinent state information
            var json = Json(surveyAnswerStateDTO.ToSurveyAnswerDTO());
            return json;
        }

        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Unlock(String ResponseId, bool RecoverLastRecordVersion)
        {
            try
            {
                SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
                SurveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
                SurveyAnswerRequest.Criteria.StatusId = 2;
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
    }
}


