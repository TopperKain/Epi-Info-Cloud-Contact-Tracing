﻿using System;
using System.Collections.Generic;
using Epi.Common.Core.Interfaces;
using Epi.DataPersistence.Constants;
using Microsoft.Azure.Documents;

namespace Epi.PersistenceServices.CosmosDB
{
    public partial class FormResponseResource : Resource
    {
        public class ChildResponseContext
        {
            public ChildResponseContext()
            {
            }

            public ChildResponseContext(FormResponseProperties formResponseProperties)
            {
                FormId = formResponseProperties.FormId;
                FormName = formResponseProperties.FormName;
                ParentFormId = formResponseProperties.ParentFormId;
                ParentFormName = formResponseProperties.ParentFormName;
                ParentResponseId = formResponseProperties.ParentResponseId;
            }
            public string FormId { get; set; }
            public string FormName { get; set; }
            public string ParentFormId { get; set; }
            public string ParentFormName { get; set; }
            public string ParentResponseId { get; set; }
        }

        public FormResponseResource()
        {
            ChildResponses = new Dictionary<string/*ParentResponseId*/, Dictionary<string/*ChildFormName*/, List<FormResponseProperties>>>();
            ChildResponseContexts = new Dictionary<string/*ResponseId*/, ChildResponseContext>();
        }

        public FormResponseProperties FormResponseProperties { get; set; }

        public Dictionary<string/*ParentResponseId*/, Dictionary<string/*ChildFormName*/, List<FormResponseProperties>>> ChildResponses { get; set; }
        public Dictionary<string/*ResponseId*/, ChildResponseContext> ChildResponseContexts { get; set; }
    }

    public partial class FormResponseProperties : IResponseContext
    {
        public FormResponseProperties()
        {
            IsNewRecord = true;
            RecStatus = RecordStatus.InProcess;
            ResponseQA = new Dictionary<string, string>();
        }
        public string ResponseId { get; set; }
        public string FormId { get; set; }
        public string FormName { get; set; }

        public string ParentResponseId { get; set; }
        public string ParentFormId { get; set; }
        public string ParentFormName { get; set; }

        public string RootResponseId { get; set; }
        public string RootFormId { get; set; }
        public string RootFormName { get; set; }

        public bool IsNewRecord { get; set; }
        public int RecStatus { get; set; }
        public int LastPageVisited { get; set; }
        public string FirstSaveLogonName { get; set; }
        public string LastSaveLogonName { get; set; }
        public DateTime FirstSaveTime { get; set; }
        public DateTime LastSaveTime { get; set; }
        public int UserOrgId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public bool IsDraftMode { get; set; }
        public bool IsLocked { get; set; }
        public string RequiredFieldsList { get; set; }
        public string HiddenFieldsList { get; set; }
        public string HighlightedFieldsList { get; set; }
        public string DisabledFieldsList { get; set; }

        public Dictionary<string, string> ResponseQA { get; set; }

        public bool IsRootForm { get { return (!string.IsNullOrEmpty(FormId) && FormId == RootFormId)
                                           || (string.IsNullOrEmpty(FormId) && string.IsNullOrEmpty(ParentFormId)); } }
        public bool IsRelatedView { get { return !IsRootForm; } }
        public bool IsRootResponse { get { return ResponseId == RootResponseId || IsRootForm; } }
        public bool IsChildResponse { get { return !IsRootResponse; } }
    }
}
