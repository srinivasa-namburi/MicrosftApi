// Copyright (c) Microsoft. All rights reserved.

import { Button, makeStyles, shorthands } from "@fluentui/react-components";
import { ReadingListAdd16Regular } from "@fluentui/react-icons";
import React from "react";
import { IChatMessage } from "../../../libs/models/ChatMessage";

const useClasses = makeStyles({
    insertButton: {
        ...shorthands.padding(0),
        ...shorthands.margin(0),
        minWidth: "auto",
        marginLeft: "auto", // align to right
    },
});

interface IInsertTextProps {
    message: IChatMessage;
}

const handleInsert = (message) => {
    return Word.run(async (context) => {
        let cursorOrSelection = context.document.getSelection();
        context.load(cursorOrSelection);
        await context.sync();
        cursorOrSelection.insertText(`${message}\n`, Word.InsertLocation.end);
        await context.sync();

        // const location = Word.InsertLocation.end;

        // insert a paragraph at the end of the document.
        // const paragraph = context.document.body.insertText(`${message}\n`, location);

        // console.log("styles in the document", context.document.getStyles().items);

        // change the paragraph color to blue.
        // paragraph.font.color = "blue";

        await context.sync();
    });
};

export const InsertText: React.FC<IInsertTextProps> = ({ message }) => {
    const classes = useClasses();

    return (
        <Button
            disabled={null}
            appearance="transparent"
            icon={<ReadingListAdd16Regular />}
            onClick={() => handleInsert(message.content)}
            title="Add to document"
            aria-label="Insert text button"
            className={classes.insertButton}
        />
    );
};
