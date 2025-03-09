import os
import requests
import shutil
import json
from pathlib import Path
import base64

def get_character_name(image_path):
    # AnimeTrace API endpoint
    url = "https://api.animetrace.com/v1/search"
    
    # Read and encode the image
    with open(image_path, 'rb') as image_file:
        base64_image = base64.b64encode(image_file.read()).decode('utf-8')
    
    # Prepare the request data
    data = {
        'model': 'anime_model_lovelive',  # Using high quality anime model
        'base64': base64_image
    }
    
    try:
        response = requests.post(url, json=data)
        print(f"\nAPI Response Status Code: {response.status_code}")
        print(f"API Response Headers: {response.headers}")
        
        # Save raw response to file
        response_file = Path(image_path).parent / f"{Path(image_path).stem}_response.json"
        
        if response.status_code == 200:
            result = response.json()
            # Save formatted JSON response
            with open(response_file, 'w', encoding='utf-8') as f:
                json.dump(result, f, ensure_ascii=False, indent=2)
            print(f"Response saved to: {response_file}")
            
            if (result.get('code') == 0 and 
                result.get('data') and 
                len(result['data']) > 0 and 
                result['data'][0].get('character') and 
                len(result['data'][0]['character']) > 0):
                
                # Print detailed result
                print(f"Recognition Result: {json.dumps(result, ensure_ascii=False, indent=2)}")
                
                # Get the first character match (highest confidence)
                first_match = result['data'][0]['character'][0]
                character = f"{first_match['character']} ({first_match['work']})"
                return character
        else:
            # Save error response
            with open(response_file, 'w', encoding='utf-8') as f:
                f.write(f"Status Code: {response.status_code}\n")
                f.write(f"Response Text: {response.text}")
            print(f"Error response saved to: {response_file}")
            
        return "unknown"
    except Exception as e:
        print(f"Error processing {image_path}: {str(e)}")
        return "unknown"

def sort_images(input_folder, output_folder):
    # Create output folder if it doesn't exist
    Path(output_folder).mkdir(parents=True, exist_ok=True)
    
    # Supported image extensions
    image_extensions = ('.jpg', '.jpeg', '.png')
    
    # Process each image in the input folder
    for file_path in Path(input_folder).glob('*'):
        if file_path.suffix.lower() in image_extensions:
            print(f"\nProcessing {file_path.name}...")
            
            # Get character name
            character_name = get_character_name(str(file_path))
            
            # Create character folder
            character_folder = Path(output_folder) / character_name
            character_folder.mkdir(exist_ok=True)
            
            # Move image to character folder
            destination = character_folder / file_path.name
            shutil.copy2(str(file_path), str(destination))
            print(f"Moved {file_path.name} to {character_name} folder")

def main():
    input_folder = r"E:\xsj666\Pictures\Saved Pictures"
    output_folder = r"C:\Users\xsj666\Desktop\test\sorted_characters"
    
    print("Starting image sorting process...")
    sort_images(input_folder, output_folder)
    print("\nSorting complete!")

if __name__ == "__main__":
    main() 