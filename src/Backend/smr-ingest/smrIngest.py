import openai

from dotenv import load_dotenv
import os
import pdfplumber
import re
from azure.search.documents import SearchClient
from azure.core.credentials import AzureKeyCredential
import json

# Load environment variables
load_dotenv()

### Functions
def extract_text_from_pdf(pdf_filename):
    text = ""
    with pdfplumber.open(pdf_filename) as pdf:
        for page in pdf.pages:
            text += page.extract_text()
    return text

def write_text_to_file(filename, text):
    try:
        with open(filename, 'w', encoding='utf-8') as file:
            file.write(text)
        print(f"Text has been written to {filename}")
    except Exception as e:
        print(f"Error writing to {filename}: {e}")

import openai
from openai.error import ServiceUnavailableError  # Import the ServiceUnavailableError

def generate_headers_array(text, api_key, max_tokens=2000):
    prompt = f"""Can you provide an array of headers and sub-headers found in the following text: {text}? If you find a table of contents, then pick the headers and sub-headers only from there. Only pick from the text given
    Return a python list of the headers and sub-headers found in the text. Include any numbering in front of the headers except if there is a number counting the lines. If there is a number counting for each line remove just that number. Return the values in the order they appear.
    """   
    try:
        openai.api_key = os.getenv("OPENAI_API_KEY")
        openai.api_type = os.getenv("OPENAI_API_TYPE")
        openai.api_base = os.getenv("OPENAI_API_ENDPOINT")
        openai.api_version = os.getenv("OPENAI_API_VERSION")
        response = openai.ChatCompletion.create(
            engine= "smrgpt4-32k",#"davinci",
            max_tokens=max_tokens,
            stop=None,
            messages=
            [{
                "role": "user",
                "content": prompt
            }]
        )
        return response.choices[0].message.content
    except ServiceUnavailableError:
        print("Server not available")
        print("Trying backup server in Canada")
        openai.api_key = os.getenv("OPENAI_API_KEY-CANDADA")
        openai.api_type = os.getenv("OPENAI_API_TYPE")
        openai.api_base = os.getenv("OPENAI_API_ENDPOINT-CANDADA")
        openai.api_version = os.getenv("OPENAI_API_VERSION")
        response = openai.ChatCompletion.create(
            engine= "smrgpt4-32k",#"davinci",
            max_tokens=max_tokens,
            stop=None,
            messages=
            [{
                "role": "user",
                "content": prompt
            }]
        )
        return response.choices[0].message.content


def extract_text_after_table_of_contents(full_text):
    # Convert the full text to lowercase for case-insensitive matching
    full_text_lower = full_text.lower()
    
    # Check if "table of contents" is in the text
    toc_index = full_text_lower.find("table of contents")
    
    # If "table of contents" is found, extract text after it
    if toc_index != -1:
        text_after_toc = full_text[toc_index + len("Table of contents"):]
        # Return the first 7000 characters of the extracted text
        return text_after_toc[:10000]
    else:
        # If "table of contents" is not found, return the first 7000 characters of the full text
        return full_text[:10000]
    
def extract_headers_and_text(text, header_array):
    headers_and_text = []
    text = text.lower()  # Convert the text to lowercase for case-insensitive search

    for i, header in enumerate(header_array):
        orgheader = header
        header = header.lower()  # Convert the header to lowercase for case-insensitive search

        # Find the first occurrence
        start_idx = text.find(header)

        # Find the second occurrence
        start_idx = text.find(header, start_idx + len(header))
        
        if i + 1 < len(header_array):
            next_header = header_array[i + 1].lower()  # Convert the next header to lowercase
        else:
            next_header = None

        if next_header:
            end_idx = text.find(next_header, start_idx + len(header))
        else:
            end_idx = len(text)
        
        extracted_text = text[start_idx:end_idx].strip()
        # Remove the header from the extracted text
        extracted_text = extracted_text.replace(header, '').strip()
        headers_and_text.append((orgheader, extracted_text))

    return headers_and_text


def create_json_files(tuples, output_directory, base_filename):
    for i, (name, data) in enumerate(tuples):
        filename = f"{output_directory}/{base_filename}_data_{i + 1}.json"
        data_dict = {'name': name, 'data': data}

        with open(filename, 'w') as json_file:
            json.dump(data_dict, json_file, indent=4)
        
        print(f"Created JSON file: {filename}")

# Define the directories where your PDF, tmp and log files are located
pdf_directory = './data/'
log_directory = './logs/'
tmp_directory = './tmp/'

# List all PDF files in the specified directory
pdf_files = [file for file in os.listdir(pdf_directory) if file.lower().endswith('.pdf')]

# Loop through the list of PDF files
for pdf_file in pdf_files:
    pdf_filename = os.path.join(pdf_directory, pdf_file)    
    full_text = extract_text_from_pdf(pdf_filename)
   
    # This below code is used to for quality control puposes, and can be deleted when the code is hardened
    # Get the base filename without the extension
    base_filename = os.path.splitext(pdf_file)[0]
    # Specify the output filename based on the input PDF filename    
    file_name_full = f'{tmp_directory}{base_filename}.txt'   
    write_text_to_file(file_name_full, full_text)
    # End of quality control code
    
    headers_string = generate_headers_array(extract_text_after_table_of_contents(full_text), os.getenv("OPENAI_API_KEY"))   
    print('headers_string:' +headers_string)
    #print(extract_text_after_table_of_contents(full_text))
    # Split the headers_string into an array of headers
    headers_array = headers_string.split('\n')

    # Remove any empty strings from the array (if present)
    headers_array = [header for header in headers_array if header.strip()]
    # Remove commas after each header
    headers_array = [header.rstrip(',') for header in headers_array]

    #Pattern for removing the qoutoation marks - used in the line clean_headers below
    quote_pattern = r'"(.*?)"'

    # Extract text within double quotes and create a new list of headers
    clean_file = f'{tmp_directory}headers_{base_filename}.txt'
    cleaned_headers = [match.group(1) for match in re.finditer(quote_pattern, headers_string)]
    print(cleaned_headers[0:10])
    #Below write is only for debugging purposes
    with open(clean_file, 'w', encoding='utf-8') as file_clean:
        for header in cleaned_headers:        
            file_clean.write(header + '\n')


    headers_and_text = extract_headers_and_text(full_text, cleaned_headers)

    with open(f'{tmp_directory}splittext_{base_filename}.txt', 'w', encoding='utf-8') as output_file:
        for header, text in headers_and_text:
            output_file.write(f"Header: {header}\n")
            output_file.write(f"Extracted Text: {text}\n\n")

    print(f"Data written to {tmp_directory}splittext_{base_filename}.txt")
    create_json_files(headers_and_text, tmp_directory, base_filename)
