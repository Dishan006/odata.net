//   OData .NET Libraries ver. 5.6.2
//   Copyright (c) Microsoft Corporation. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

namespace Microsoft.Data.Edm.Library
{
    /// <summary>
    /// Represents a reference to an EDM row type.
    /// </summary>
    public class EdmRowTypeReference : EdmTypeReference, IEdmRowTypeReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdmRowTypeReference"/> class.
        /// </summary>
        /// <param name="rowType">Type that describes this value.</param>
        /// <param name="isNullable">Denotes whether the type can be nullable.</param>
        public EdmRowTypeReference(IEdmRowType rowType, bool isNullable)
            : base(rowType, isNullable)
        {
        }
    }
}