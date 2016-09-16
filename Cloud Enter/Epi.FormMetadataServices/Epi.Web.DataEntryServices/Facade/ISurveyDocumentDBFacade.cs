﻿using Epi.Cloud.Common.Metadata;
using Epi.Cloud.DataEntryServices.Model;
using Epi.Web.Enter.Common.DTO;
using Epi.Web.Enter.Common.Message;
using System.Collections.Generic;
using System.Threading.Tasks;
using Epi.Cloud.Common.EntityObjects;

namespace Epi.Cloud.DataEntryServices.Facade
{
    public interface ISurveyStoreDocumentDBFacade
    {
        //Insert new record  survey response data in to table storage.

        bool DoesResponseExist(string responseId);

        bool UpdateResponseStatus(string responseId, int recordStatus);

        int GetFormResponseCount(string formId);

        FormResponseDetail GetFormResponseState(string responseId);

        FormResponseDetail GetFormResponseByResponseId(string responseId);

        Task<bool> InsertSurveyResponseToDocumentDBStoreAsync(SurveyInfoModel surveyInfoModel, string responseId, MvcDynamicForms.Form form, Epi.Web.Enter.Common.DTO.SurveyAnswerDTO surveyAnswerDTO, bool IsSubmited, bool IsSaved, int PageNumber, int UserId);
         
        PageResponseDetail ReadSurveyAnswerByResponseID(string surveyId, string responseId, int pageId);

        SurveyAnswerResponse DeleteResponse(FormDocumentDBEntity SARequest);

        bool SaveFormPropertiesToDocumentDB(SurveyAnswerRequest request);
        SurveyAnswerResponse GetSurveyAnswerResponse(string responseId);
        SurveyAnswerResponse GetSurveyAnswerResponse(string responseId, int UserId);
        IEnumerable<SurveyResponse> GetAllResponsesContainingFields(IDictionary<int, FieldDigest> gridFields);
        FormsHierarchyDTO GetChildRecordByChildFormId(string childFormId, string relateParentId, IDictionary<int, FieldDigest> gridFields);
    }
}
