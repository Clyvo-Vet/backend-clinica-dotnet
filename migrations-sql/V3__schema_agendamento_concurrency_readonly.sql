declare
   l_nullable user_tab_columns.nullable % type;
begin 
   select nullable into l_nullable 
   from user_tab_columns 
  where table_name = 'TRIAGEM_LUNA' 
  and column_name = 'ST_ENCAMINHADO_VET' 
;
   if l_nullable = 'N' then 
        EXECUTE IMMEDIATE 'ALTER TABLE "TRIAGEM_LUNA" MODIFY "ST_ENCAMINHADO_VET" NUMBER(10) ';
 else 
        EXECUTE IMMEDIATE 'ALTER TABLE "TRIAGEM_LUNA" MODIFY "ST_ENCAMINHADO_VET" NUMBER(10) NOT NULL';
 end if;
end;
/

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES (N'20260511124904_Schema_v3_AgendamentoConcurrencyReadOnlyInterceptor', N'10.0.7')
/

