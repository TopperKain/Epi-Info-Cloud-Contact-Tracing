﻿using System.Collections.Generic;
using Epi.Web.Enter.Common.BusinessObject;

namespace Epi.Web.Enter.Interfaces.DataInterface
{
    public interface IFormInfoDao
    {
        List<FormInfoBO> GetFormInfo(int userId, int currentOrgId);
        FormInfoBO GetFormByFormId(string formId, bool getMetadata, int userId);
        FormInfoBO GetFormByFormId(string formId);
        bool GetEwavLiteToggleSwitch(string formId, int userId);
        bool HasDraftRecords(string formId);
    }
}
