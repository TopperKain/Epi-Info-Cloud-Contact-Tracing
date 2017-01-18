﻿using System;
using System.Collections.Generic;
using System.Linq;
using Epi.Cloud.Common.Metadata;
using Epi.DataPersistence.Constants;
using Epi.DataPersistence.DataStructures;
using Epi.FormMetadata.DataStructures;

namespace Epi.Web.MVC.Utility
{
    public class SurveyResponseHelper
    {
        private IEnumerable<AbridgedFieldInfo> _pageFields;
        private string _requiredList = "";

        private Dictionary<string, string> _responseQA = new Dictionary<string, string>();

        private MetadataAccessor _metadataAccessor;
        private MetadataAccessor MetadataAccessor
        {
            get { return _metadataAccessor = _metadataAccessor ?? new MetadataAccessor(); }
        }

        public string RequiredList
        {
            get { return _requiredList; }
            set { _requiredList = value; }
        }

        public SurveyResponseHelper(IEnumerable<AbridgedFieldInfo> pageFields, string requiredList)
        {
            _pageFields = pageFields;
            _requiredList = requiredList;
        }

        public SurveyResponseHelper()
        {
        }

        public void Add(MvcDynamicForms.Form pForm)
        {
            _responseQA.Clear();
            foreach (var field in pForm.InputFields)
            {
                if (!field.IsPlaceHolder)
                {
                    _responseQA[field.Title] = field.Response;
                }
            }
        }

        public FormResponseDetail CreateResponseDetail(string formId, bool addRoot, int currentPage, string pageId, string responseId)
        {
            var formName = MetadataAccessor.GetFormDigest(formId).FormName;
            var formResponseDetail = new FormResponseDetail
            {
                GlobalRecordID = responseId,
                IsNewRecord = true,
				RecStatus = RecordStatus.InProcess,
                FormId = formId,
                FormName = formName,
                LastPageVisited = currentPage == 0 ? 1 : currentPage
            };

            if (!String.IsNullOrWhiteSpace(pageId))
            {
                var pageResponseDetail = new PageResponseDetail
                {
                    PageId = Convert.ToInt32(pageId),
                    PageNumber = currentPage,
                    ResponseQA = _responseQA
                };
                formResponseDetail.AddPageResponseDetail(pageResponseDetail);
            }

            return formResponseDetail;
        }

        public FormResponseDetail CreateResponseDocument(PageDigest[] pageDigests)
        {
            int numberOfPages = pageDigests.Length;

            var firstPageDigest = pageDigests.First();
            var formId = firstPageDigest.FormId;
            var formName = firstPageDigest.FormName;

            FormResponseDetail formResponseDetail = new FormResponseDetail { FormId = formId, FormName = formName, LastPageVisited = 1 };
            foreach (var pageDigest in pageDigests)
            {
                var fieldNames = pageDigest.FieldNames;
                var pageResponseDetail = new PageResponseDetail
                {
                    PageId = pageDigest.PageId,
                    PageNumber = pageDigest.PageNumber,
                    
                    ResponseQA = fieldNames.Select(x => new { Key = x.ToLower(), Value = string.Empty }).ToDictionary(n => n.Key, v => v.Value)
                };
                SetRequiredList(pageDigest.Fields);
                formResponseDetail.AddPageResponseDetail(pageResponseDetail);
            }
            return formResponseDetail;
        }


        public void SetRequiredList(AbridgedFieldInfo[] fields)
        {
            foreach (var field in fields)
            {
                if (field.IsRequired == true)
                {
                    if (this.RequiredList != "")
                    {
                        this.RequiredList = this.RequiredList + "," + field.FieldName.ToLower();
                    }
                    else
                    {
                        this.RequiredList = field.FieldName.ToLower();
                    }
                }
            } 
        }

    }
}
