/**
 * Inserts paragraphs on the specified location
 * @param  {} text
 */
export async function insertDocumentText(text) {
    await Word.run(async (context) => {

        // Create a proxy object for the document body.
        const body = context.document.body;

        // Queue a command to insert text at the end of the document body.
        //body.insertText(text, Word.InsertLocation.end);
        body.insertParagraph(text, Word.InsertLocation.end);

        // Synchronize the document state by executing the queued commands,
        // and return a promise to indicate task completion.
        await context.sync();
    })
        .catch(function (error) {
            console.log('Error: ' + JSON.stringify(error));
            if (error instanceof OfficeExtension.Error) {
                console.log('Debug info: ' + JSON.stringify(error.debugInfo));
            }
        });
}

export async function getDocumentText() {
    let documentText = "";
    await Word.run(async (context) => {
        // Create a proxy object for the document body.
        const body = context.document.body;

        // Queue a command to load the text in document body.
        body.load("text");

        // Synchronize the document state by executing the queued commands, and return a promise to indicate task completion.
        await context.sync();

        console.log("Body contents (text): " + body.text);
        documentText=body.text;
    });

    return documentText;
}

