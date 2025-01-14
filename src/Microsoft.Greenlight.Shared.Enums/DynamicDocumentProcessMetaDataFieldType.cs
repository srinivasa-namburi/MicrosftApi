namespace Microsoft.Greenlight.Shared.Enums
{
    public enum DynamicDocumentProcessMetaDataFieldType
    {
        /// <summary>
        /// Text field. Single line input.
        /// </summary>
        Text = 100,
    
        /// <summary>
        /// Multiline text field. Multiple lines of input.
        /// </summary>
        MultilineText = 200,
    
        /// <summary>
        /// Number field. Numeric input.
        /// </summary>
        Number = 300,
    
        /// <summary>
        /// Date field. Date input.
        /// </summary>
        Date = 400,
    
        /// <summary>
        /// Time field. Time input.
        /// </summary>
        Time = 500,
    
        /// <summary>
        /// Date and time field. Date and time input.
        /// </summary>
        DateTime = 600,
    
        /// <summary>
        /// Boolean field. Checkbox input.
        /// </summary>
        BooleanCheckbox = 700,

        /// <summary>
        /// Boolean field. Switch/toggle input.
        /// </summary>
        BooleanSwitchToggle = 800,

        /// <summary>
        /// File field. File upload input. Files get uploaded to blob storage. Value of the result is the URL of the file'
        /// proxied through the File controller in the API.
        /// </summary>
        File = 900,
    
        /// <summary>
        /// List of possible values. Multiple selection input. Display as a set of checkboxes.
        /// </summary>
        MultiSelectWithPossibleValues = 1000,
    
        /// <summary>
        /// Single selection from a list of possible values. Display as a dropdown.
        /// </summary>
        SelectRadioButton = 1100,
    
        /// <summary>
        /// Dropdown field. Selection input.
        /// </summary>
        SelectDropdown = 1200,

        /// <summary>
        /// Map component selector field. Results in two fields in an array: Latitude and Longitude. They are named "Name + Latitude" and "Name + Longitude".
        /// </summary>
        MapComponent = 1300
    }
}