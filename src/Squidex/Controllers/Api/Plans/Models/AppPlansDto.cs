﻿// ==========================================================================
//  AppPlansDto.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System.Collections.Generic;

namespace Squidex.Controllers.Api.Plans.Models
{
    public class AppPlansDto
    {
        /// <summary>
        /// The available plans.
        /// </summary>
        public List<PlanDto> Plans { get; set; }

        /// <summary>
        /// The current plan id.
        /// </summary>
        public string CurrentPlanId { get; set; }

        /// <summary>
        /// The plan owner.
        /// </summary>
        public string PlanOwner { get; set; }

        /// <summary>
        /// Indicates if there is a billing portal.
        /// </summary>
        public bool HasPortal { get; set; }

        /// <summary>
        /// Indicates if the user has payment options entered so that the plan can be changed.
        /// </summary>
        public bool HasConfigured { get; set; }
    }
}
